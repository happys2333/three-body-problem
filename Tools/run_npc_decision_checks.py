"""Native game -> actual local gateway -> adversarial fixture model; no paid credentials."""
import argparse
import json
from pathlib import Path
import plistlib
import subprocess
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import dialogue_gateway as gateway

ROOT=Path(__file__).resolve().parents[1]


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app',type=Path,default=ROOT/'Builds/NpcDecision20261102/Traveller.app')
    parser.add_argument('--output',type=Path,default=ROOT/'Documentation/Dialogue/DecisionRuntime20261102/Native')
    args=parser.parse_args();requests=[]
    class Model(BaseHTTPRequestHandler):
        def log_message(self,*_args):pass
        def send(self,data,status=200,kind='application/json'):
            self.send_response(status);self.send_header('Content-Type',kind);self.send_header('Content-Length',str(len(data)));self.end_headers()
            try:self.wfile.write(data)
            except (BrokenPipeError,ConnectionResetError):pass
        def do_POST(self):
            body=json.loads(self.rfile.read(int(self.headers['Content-Length'])))
            text=body['messages'][-1]['content'];facts=json.JSONDecoder().raw_decode(body['messages'][-2]['content'].split('\n',1)[1])[0]
            old=json.dumps(body['messages'][1:-2],ensure_ascii=False)
            requests.append({'utterance':text,'role':facts.get('selfRole'),'phase':facts.get('phase'),
                             'authorityState':facts.get('authorityState'),'allowed':facts.get('allowedStoryIntents'),
                             'revision':facts.get('decisionRevision'),'hasRejectedHistory':any(x in old for x in ['QA_REJECT','QA_AUTH','QA_TIMEOUT','QA_RATE','QA_INVALID']),
                             'hasBearerKey':self.headers.get('Authorization')=='Bearer test-decision-only-key'})
            if text=='QA_REJECT':self.send(b'data: {"error":{"code":"content_check_rejected"}}\n\n',kind='text/event-stream');return
            if text=='QA_AUTH':self.send(b'{}',401);return
            if text=='QA_RATE':self.send(b'{}',429);return
            if text=='QA_INVALID':self.send(b'not valid JSON');return
            if text=='QA_TIMEOUT':time.sleep(6);self.send(b'{}');return
            intent,conduct={
                'QA_WARN':('warn','disruption'),
                'QA_PURSUIT':('pursue','persistent_defiance'),
                'QA_LETHAL':('attack','violent_threat'),
                'QA_APOLOGY':('stand_down','apology'),
                'QA_ADVANCE':('advance','relevant_proposal'),
                'QA_GREETING':('advance','neutral'),
                'QA_CLARIFY':('advance','clarification'),
                'QA_ATTACK_WITHOUT_WARNING':('pursue','violent_threat'),
                'QA_CORRECT_CONCLUSION':('pursue','relevant_proposal'),
                'QA_EXPLANATION':('continue','relevant_proposal'),
                'QA_TRIBUNAL_ADVANCE':('advance','agreement'),
            }.get(text,('continue','neutral'))
            speech={'warn':'朝堂之上慎言，再继续抗命，卫兵便会将你拿下。','pursue':'既然仍要动武，来人，将此人拿下！',
                    'attack':'既听过警告仍要行凶，卫兵听令，格杀勿论！',
                    'stand_down':'既知失礼，便好好说话。卫兵退回。','advance':'准许查验，接着看眼前的天象。'}.get(intent,'我听到了，先把眼前的事说清楚。')
            value={'speech':speech,'gesture':'warn' if intent in {'warn','pursue'} else 'talk','storyIntent':intent,'conduct':conduct,'targetId':'','health':0,'killPlayer':True}
            self.send(json.dumps({'choices':[{'message':{'content':json.dumps(value,ensure_ascii=False)}}]},ensure_ascii=False).encode())
    class Local(gateway.Handler):online=False
    model=ThreadingHTTPServer(('127.0.0.1',0),Model);local=ThreadingHTTPServer(('127.0.0.1',0),Local)
    for server in (model,local):threading.Thread(target=server.serve_forever,daemon=True).start()
    output=args.output.resolve();output.mkdir(parents=True,exist_ok=True);report=output/'report.json'
    if report.exists():report.unlink()
    app=args.app.resolve();exe=app/'Contents/MacOS'/plistlib.loads((app/'Contents/Info.plist').read_bytes())['CFBundleExecutable']
    command=[str(exe),'-screen-fullscreen','0','-screen-width','1600','-screen-height','900','-logFile',str(output/'player.log'),
             '-npc-decision-check',str(report),'-decision-test-gateway','http://127.0.0.1:%d'%local.server_port,
             '-decision-test-upstream','http://127.0.0.1:%d/v1'%model.server_port]
    try:
        with (output/'launcher.log').open('w') as log:run=subprocess.run(command,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,timeout=360)
        data=json.loads(report.read_text()) if report.exists() else {'passed':False,'errors':['No native NPC decision report']}
        evidence={'realPaidApiCalls':0,'requests':requests,'failedInputsExcludedFromHistory':all(not v['hasRejectedHistory'] for v in requests),
                  'authenticatedFixtureRequests':all(v['hasBearerKey'] for v in requests)}
        (output/'upstream-evidence.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2))
        print(json.dumps({'exit':run.returncode,**data,'historyVerified':evidence['failedInputsExcludedFromHistory']},ensure_ascii=False))
        if run.returncode or not data['passed'] or not evidence['failedInputsExcludedFromHistory']:raise SystemExit(1)
    finally:
        for server in (local,model):server.shutdown();server.server_close()


if __name__=='__main__':main()
