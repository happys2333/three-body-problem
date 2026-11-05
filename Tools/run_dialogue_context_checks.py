"""Native character memory + current facts -> actual local HTTP -> deterministic test model."""
import argparse
import json
from pathlib import Path
import subprocess
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import dialogue_gateway as gateway
import dialogue_context

ROOT=Path(__file__).resolve().parents[1]


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app',type=Path,default=ROOT/'Builds/ActOneTraveller.app')
    parser.add_argument('--output',type=Path,default=ROOT/'Documentation/Dialogue/2026-11-02/Native')
    args=parser.parse_args();requests=[]
    class Model(BaseHTTPRequestHandler):
        def log_message(self,*args):pass
        def do_POST(self):
            body=json.loads(self.rfile.read(int(self.headers['Content-Length'])))
            messages=body['messages'];raw_facts=messages[-2]['content'].split('\n',1)[1]
            facts=json.JSONDecoder().raw_decode(raw_facts)[0]
            text=json.dumps(messages,ensure_ascii=False)
            history=json.dumps(messages[1:-2],ensure_ascii=False)
            person=facts['selfRole'];knows='我叫阿遥，是个迷路的木匠。' in history
            utterance=messages[-1]['content']
            witnessed=facts.get('witnessedObservations',{})
            record={'role':person,'scene':facts['scene'],'historyMessages':len(messages)-3,'hasOriginalIntroduction':knows,
                    'hasPrivateNotebook':'PRIVATE_NOTEBOOK_CONTEXT_QA' in text,'hasOldNotebookRecord':'PRIVATE_OLD_RUN_CONTEXT_QA' in text,
                    'knownEvents':len(facts.get('knownEvents',[])),'skyVisible':facts.get('skyVisible'),
                    'activity':facts.get('currentActivity'),'model':body['model'],'maxOutputTokens':body['max_tokens'],
                    'observationIds':list(witnessed),'toolOffered':body.get('tool_choice')=='auto'}
            requests.append(record)
            speech='阿遥，你说过自己是迷路的木匠。我记得。' if person=='wenwang' and knows else '寡人没有听见那段私下交谈。把你愿意说的事讲清楚。'
            result={'speech':speech,'gesture':'think','storyIntent':'continue'}
            choice={'message':{'content':json.dumps(result,ensure_ascii=False)}}
            if utterance=='我刚才看到三个太阳。':
                choice['message']['content']=json.dumps({'speech':'我只看见一轮。','observationId':'gobi_giant_sun'})
            elif utterance=='我刚才看到一轮特别大的太阳。' and 'gobi_giant_sun' in witnessed:
                choice={'finish_reason':'tool_calls','message':{'content':None,'tool_calls':[
                    {'id':'qa-observation','type':'function','function':{
                        'name':'record_observation','arguments':'{"id":"gobi_giant_sun"}'}}]}}
            raw=json.dumps({'choices':[choice]},ensure_ascii=False).encode()
            self.send_response(200);self.send_header('Content-Length',str(len(raw)));self.end_headers();self.wfile.write(raw)
    class Local(gateway.Handler):online=False
    model=ThreadingHTTPServer(('127.0.0.1',0),Model);local=ThreadingHTTPServer(('127.0.0.1',0),Local)
    for server in (model,local):threading.Thread(target=server.serve_forever,daemon=True).start()
    output=args.output.resolve();output.mkdir(parents=True,exist_ok=True);report=output/'report.json'
    if report.exists():report.unlink()
    app=args.app.resolve()
    command=['open','-n','-W','-a',str(app),'--args','-screen-fullscreen','0','-screen-width','1600','-screen-height','900','-logFile',str(output/'player.log'),
             '-dialogue-context-check',str(report),'-context-test-gateway','http://127.0.0.1:%d'%local.server_port,
             '-context-test-upstream','http://127.0.0.1:%d/v1'%model.server_port]
    try:
        with (output/'launcher.log').open('w') as log:run=subprocess.run(command,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,timeout=120)
        data=json.loads(report.read_text()) if report.exists() else {'passed':False,'errors':['No native context report']}
        wen=[r for r in requests if r['role']=='wenwang'];kings=[r for r in requests if r['role']=='king']
        verified=(len(wen)==4 and len(kings)==1 and all(r['hasOriginalIntroduction'] and r['historyMessages']>12 for r in wen)
                  and [r['observationIds'] for r in wen]==[[],['gobi_giant_sun'],['gobi_giant_sun'],[]]
                  and [r['toolOffered'] for r in wen]==[False,True,True,False]
                  and not kings[0]['hasOriginalIntroduction'] and not kings[0]['skyVisible']
                  and all(not r['hasPrivateNotebook'] and not r['hasOldNotebookRecord'] and r['maxOutputTokens']==320 for r in requests))
        evidence={'realPaidApiCalls':0,'tokenizerAvailable':dialogue_context.official_tokenizer() is not None,'requests':requests,'boundariesVerified':verified}
        (output/'upstream-evidence.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n')
        print(json.dumps({'exit':run.returncode,'upstreamVerified':verified,**data},ensure_ascii=False))
        if run.returncode or not data['passed'] or not verified:raise SystemExit(1)
    finally:
        for server in (local,model):server.shutdown();server.server_close()


if __name__=='__main__':main()
