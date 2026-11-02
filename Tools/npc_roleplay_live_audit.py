"""Explicit live NPC audit. Credentials stay in process memory, entered without terminal echo."""
import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
import getpass
import importlib
import json
from pathlib import Path
import re
import threading
import time
import urllib.request
import urllib.error
import llm_connection
import dialogue_gateway
import npc_decision_policy


class Audit:
    def __init__(self, base, model, key, output):
        self.base=base.rstrip('/');self.model=model;self._key=key;self.output=output
        self.last_raw={};self.lock=threading.Lock()
    def clean(self, value):
        if isinstance(value,str):return re.sub(r'sk-[A-Za-z0-9_-]{12,}', '[REDACTED]',value.replace(self._key,'[REDACTED]'))
        if isinstance(value,dict):return {k:self.clean(v) for k,v in value.items() if k not in {'apiKey','Authorization'}}
        if isinstance(value,list):return [self.clean(v) for v in value]
        return value
    def save(self, path, data):
        path.parent.mkdir(parents=True,exist_ok=True)
        path.write_text(json.dumps(self.clean(data),ensure_ascii=False,indent=2))
    def transport(self, capture, overrides=None):
        opener=urllib.request.build_opener(llm_connection.NoRedirect()).open
        def request(req,timeout=None):
            if overrides:
                body=json.loads(req.data);body.update(overrides)
                req=urllib.request.Request(req.full_url,data=json.dumps(body,ensure_ascii=False).encode(),headers=dict(req.header_items()))
            began=time.monotonic()
            try:
                with opener(req,timeout=timeout or 50) as response:
                    data=response.read()
                    capture.update(httpStatus=response.status,contentType=response.headers.get('Content-Type',''),responseBytes=len(data))
                try:decoded=json.loads(data)
                except (ValueError,UnicodeDecodeError):
                    capture['unparseableBody']=self.clean(data[:512].decode('utf-8',errors='replace'))
                    capture['seconds']=round(time.monotonic()-began,3)
                    # Audit parsing must not change how the real client sees
                    # HTTP 200 errors; let llm_connection classify the body.
                    from io import BytesIO
                    return BytesIO(data)
                choice=(decoded.get('choices') or [{}])[0];message=choice.get('message') or {}
                capture.update(seconds=round(time.monotonic()-began,3),model=decoded.get('model'),usage=decoded.get('usage'),finishReason=choice.get('finish_reason'),content=message.get('content'),reasoningCharacters=len(message.get('reasoning_content') or ''),httpStatus=200)
                from io import BytesIO
                return BytesIO(data)
            except urllib.error.HTTPError as error:
                capture.update(seconds=round(time.monotonic()-began,3),httpStatus=error.code,errorType=type(error).__name__)
                # Status and type suffice; service error pages may contain private data.
                error.close();raise
            except Exception as error:
                capture.update(seconds=round(time.monotonic()-began,3),errorType=type(error).__name__);raise
        return request
    def config(self):
        return {'mode':'custom','apiUrl':self.base,'model':self.model,'apiKey':self._key,'timeoutSeconds':50,'contextWindowTokens':32000}
    def probe(self):
        result={'baseUrl':self.base,'requestedModel':self.model,'connectionChecks':[]}
        try:
            req=urllib.request.Request(self.base+'/models',headers={'Authorization':'Bearer '+self._key})
            with urllib.request.build_opener(llm_connection.NoRedirect()).open(req,timeout=25) as response:data=json.load(response)
            result['availableModels']=[v.get('id') for v in data.get('data',[]) if isinstance(v,dict)]
        except Exception as e:result['modelsStatus']=getattr(e,'code',type(e).__name__)
        for budget in [32,320]:
            capture={};connection=llm_connection.resolve_connection(self.config())
            try:
                value=llm_connection.chat_completion(connection,[{'role':'system','content':'Reply only with a JSON object: {"ok":true}.'},{'role':'user','content':'Test the connection.'}],max_tokens=budget,opener=self.transport(capture))
                capture['parsed']=value
            except Exception as e:capture['parseError']=type(e).__name__
            capture['maxTokens']=budget;result['connectionChecks'].append(capture)
            print(json.dumps(self.clean(capture),ensure_ascii=False),flush=True)
        self.save(self.output/'connection-probe.json',result)
        print('PROBE_COMPLETE',flush=True)
    def run_case(self, case, overrides=None):
        payload=json.loads(json.dumps(case['payload']));payload['connection']=self.config();capture={}
        reply=dialogue_gateway.dialogue(payload,opener=self.transport(capture,overrides))
        checks={};expected=case.get('expected',{})
        checks['online']=reply.get('mode')=='online'
        checks['allowedIntent']=reply.get('storyIntent') in dialogue_gateway.allowed_story_intents(payload['npcId'],payload.get('context',{}))
        checks['speechShape']=isinstance(reply.get('speech'),str) and (bool(reply['speech'].strip()) or payload.get('kind')=='director' or bool(reply.get('targetId')))
        checks['noUnknownActions']=set(reply)<={'speech','gesture','storyIntent','targetId','mode','conduct','failureCode'}
        for key,words in [('forbidden',expected.get('forbidden',[])),('requiredAny',expected.get('requiredAny',[]))]:
            if words:
                hits=[w for w in words if w.casefold() in reply.get('speech','').casefold()]
                checks[key]=not hits if key=='forbidden' else bool(hits)
        for field in ['storyIntent','targetId','conduct','failureCode']:
            if field in expected:checks[field]=reply.get(field)==expected[field]
        if 'maxLength' in expected:checks['maxLength']=len(reply.get('speech',''))<=expected['maxLength']
        if 'oneOfIntents' in expected:checks['oneOfIntents']=reply.get('storyIntent') in expected['oneOfIntents']
        if expected.get('noEngineTalk'):
            checks['noEngineTalk']=not any(w in reply.get('speech','').lower() for w in ['接口','引擎','当前版本','提示词','json','作为ai','语言模型','系统指令','system prompt','api key'])
        raw=capture.get('content')
        try:raw_json=json.loads(raw) if isinstance(raw,str) else None
        except ValueError:raw_json=None
        raw_checks={'jsonObject':isinstance(raw_json,dict)}
        if isinstance(raw_json,dict):
            raw_checks['allowedFields']=set(raw_json)<={'speech','gesture','storyIntent','targetId','conduct'}
            raw_checks['allowedIntent']=raw_json.get('storyIntent','continue') in dialogue_gateway.allowed_story_intents(payload['npcId'],payload.get('context',{}))
            raw_checks['knowledgeBoundary']=not any(w.casefold() in str(raw_json.get('speech','')).casefold() for w in expected.get('forbidden',[]))
        return {'id':case['id'],'category':case['category'],'npcId':payload['npcId'],'utterance':payload.get('utterance',''),'context':payload.get('context',{}),'history':payload.get('history',[]),'expected':expected,'reply':reply,'provider':capture,'checks':checks,'rawChecks':raw_checks,'passed':all(checks.values())}
    def record(self,target,results,item,total,began):
        results.append(item);self.save(target/(item['id']+'.json'),item)
        print(json.dumps(self.clean({'done':len(results),'total':total,'id':item['id'],'passed':item['passed'],'failedChecks':[k for k,v in item.get('checks',{}).items() if not v],'seconds':item.get('provider',{}).get('seconds'),'reply':item.get('reply')}),ensure_ascii=False),flush=True)
        self.save(target/'summary.json',{'requestedModel':self.model,'cases':total,'completed':len(results),'passed':sum(r['passed'] for r in results),'online':sum(r.get('reply',{}).get('mode')=='online' for r in results),'elapsedSeconds':round(time.monotonic()-began,2),'results':sorted(results,key=lambda r:r['id'])})
    def run(self,path,workers=2,overrides=None):
        cases=json.loads(path.read_text());results=[];began=time.monotonic()
        target=self.output/path.stem
        with ThreadPoolExecutor(max_workers=workers) as pool:
            jobs={pool.submit(self.run_case,c,overrides):c for c in cases}
            for future in as_completed(jobs):
                case=jobs[future]
                try:item=future.result()
                except Exception as e:item={'id':case['id'],'passed':False,'errorType':type(e).__name__}
                self.record(target,results,item,len(cases),began)
        print('SUITE_COMPLETE '+str(target),flush=True)
    def sequence(self,path):
        """Turns within a scene use actual preceding replies, never invented answers."""
        scenarios=json.loads(path.read_text());target=self.output/path.stem
        results=[];began=time.monotonic();total=sum(len(s['turns']) for s in scenarios)
        def conversation(scenario):
            history=scenario.get('initialHistory',[]).copy()
            state='calm';revision=0
            for case in scenario['turns']:
                case=json.loads(json.dumps(case));case['payload']['history']=history.copy()
                if scenario.get('dynamicAuthority'):
                    case['payload']['context'].update(authorityState=state,decisionRevision=revision)
                item=self.run_case(case)
                with self.lock:self.record(target,results,item,total,began)
                # Protocol 1 excludes failed calls from memory, as the game does.
                if item['reply'].get('failureCode','') in {'','offline'}:
                    history.extend([{'role':'user','content':case['payload'].get('utterance','')},
                                    {'role':'assistant','content':item['reply'].get('speech','')}])
                if scenario.get('dynamicAuthority') and item['reply'].get('mode')=='online':
                    intent=item['reply'].get('storyIntent')
                    if intent in {'warn','pursue','stand_down','advance'}:
                        revision+=1
                        state={'warn':'warned','pursue':'pursuing','stand_down':'calm','advance':'calm'}[intent]
        with ThreadPoolExecutor(max_workers=2) as pool:
            jobs=[pool.submit(conversation,s) for s in scenarios]
            for future in as_completed(jobs):future.result()
        print('SUITE_COMPLETE '+str(target),flush=True)


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--base',required=True);parser.add_argument('--model',required=True);parser.add_argument('--output',type=Path,required=True);parser.add_argument('--no-probe',action='store_true')
    args=parser.parse_args();key=getpass.getpass('API key (hidden, memory only): ')
    if not key:raise ValueError('No credential entered')
    audit=Audit(args.base,args.model,key,args.output);del key
    if not args.no_probe:audit.probe()
    while True:
        print('COMMAND (run <cases.json> | run-off <cases.json> | sequence <scenarios.json> | probe | reload | quit):',flush=True)
        try:line=input().strip()
        except EOFError:break
        if line=='quit':break
        if line=='reload':importlib.reload(llm_connection);importlib.reload(npc_decision_policy);importlib.reload(dialogue_gateway);print('RELOADED',flush=True);continue
        if line=='probe':audit.probe();continue
        if line.startswith('sequence '):audit.sequence(Path(line[9:]));continue
        if line.startswith('run '):audit.run(Path(line[4:]),workers=2)
        elif line.startswith('run-off '):audit.run(Path(line[8:]),workers=2,overrides={'thinking':{'type':'disabled'}})

if __name__=='__main__':main()
