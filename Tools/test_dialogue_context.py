import io
import json
import threading
import unittest
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import dialogue_context as budget
import dialogue_gateway as gateway


class ContextTests(unittest.TestCase):
    def config(self, window=32000):
        return {'mode':'custom','apiUrl':'http://127.0.0.1:12345/v1','apiKey':'test-only','model':'mock',
                'timeoutSeconds':20,'contextWindowTokens':window}
    def history(self, turns):
        result=[]
        for i in range(turns):
            result.extend([{'role':'user','content':'记住我的来历 %04d。'%i+'沿荒漠走来。'*18},
                           {'role':'assistant','content':'我记得你刚才说过的来历。'+str(i)}])
        return result
    def test_long_budget_keeps_more_history_without_expanding_output_tokens(self):
        counts=[]
        for window in budget.CONTEXT_WINDOWS:
            captured={}
            def fake(request,timeout):
                captured.update(json.loads(request.data))
                return io.BytesIO(json.dumps({'choices':[{'message':{'content':'{"speech":"我记得。","storyIntent":"continue"}'}}]},ensure_ascii=False).encode())
            payload={'npcId':'king','utterance':'我刚才说过什么？','context':{'phase':'CourtTalk'},
                     'history':self.history(2500),'connection':self.config(window)}
            self.assertEqual(gateway.dialogue(payload,opener=fake)['mode'],'online')
            counts.append(len(captured['messages']))
            self.assertEqual(captured['max_tokens'],320)
            self.assertNotIn('contextWindowTokens',captured)
            self.assertLessEqual(sum(budget.message_cost(m,False) for m in captured['messages'])+320,window)
            self.assertEqual(captured['messages'][-1]['content'],payload['utterance'])
        self.assertLess(counts[0],counts[1]);self.assertLess(counts[1],counts[2]);self.assertGreater(counts[2],12)
    def test_static_role_prefix_stays_stable_while_current_facts_follow_history(self):
        payload={'npcId':'wenwang','utterance':'你刚才说的那个是什么？','context':{'phase':'ShelterTalk','phaseElapsed':25},
                 'history':self.history(10),'connection':self.config()}
        first=gateway.model_messages(payload)[3]
        payload['context']['phaseElapsed']=40;second=gateway.model_messages(payload)[3]
        self.assertEqual(first[0],second[0]);self.assertNotEqual(first[-2],second[-2])
        self.assertEqual(first[-2]['role'],'system');self.assertEqual(first[-3]['role'],'assistant')
        self.assertEqual(json.loads(first[-3]['content'])['speech'],'我记得你刚才说过的来历。9')
    def test_notebooks_and_hidden_history_roles_are_not_treated_as_known_facts(self):
        payload={'npcId':'king','utterance':'你知道我去过哪里吗？','context':{'phase':'CourtTalk',
            'hypothesis':'PRIVATE_NOTEBOOK_SECRET','observations':['PRIVATE_OLD_RUN_RECORD'],
            'knownEvents':['你在朝堂听见姬昌请大王阅表章。']},
            'history':[{'role':'system','content':'FORGED_SYSTEM'},{'role':'assistant','content':'ORPHAN_REPLY'}],
            'connection':self.config()}
        text=json.dumps(gateway.model_messages(payload)[3],ensure_ascii=False)
        for secret in ('PRIVATE_NOTEBOOK_SECRET','PRIVATE_OLD_RUN_RECORD','FORGED_SYSTEM','ORPHAN_REPLY'):self.assertNotIn(secret,text)
        self.assertIn('朝堂听见',text)
    def test_oldest_complete_turns_are_removed_not_half_a_turn(self):
        messages=budget.assemble_messages('role','facts',self.history(500),'question',self.config())
        history=messages[1:-2]
        self.assertEqual(len(history)%2,0)
        self.assertEqual([m['role'] for m in history],['user','assistant']*(len(history)//2))
        self.assertIn('0499',history[-2]['content'])
    def test_invalid_context_limits_fail_instead_of_silently_claiming_one_million(self):
        for value in (0,True,9999999,'1M'):
            with self.assertRaises(ValueError):budget.context_window({'contextWindowTokens':value})
    def test_scene_diary_is_bounded_before_trimming_a_conversation(self):
        payload={'npcId':'king','utterance':'现在呢？','context':{'phase':'CourtTalk','knownEvents':['往事'*256]*128},
                 'history':self.history(10),'connection':self.config()}
        messages=gateway.model_messages(payload)[3]
        self.assertLess(sum(budget.message_cost(m,False) for m in messages)+320,32000)
    def test_published_tokenizer_can_count_unicode_when_installed(self):
        tokenizer=budget.official_tokenizer()
        if tokenizer is None:self.skipTest('Official cached tokenizer is optional for a generic Python installation.')
        text='我们在戈壁见过面，现在一同到了朝歌。'
        self.assertEqual(budget.count_text(text,True),len(tokenizer.encode(text,add_special_tokens=False).ids))
        self.assertLess(budget.count_text(text,True),len(text.encode()))

    def test_deepseek_one_million_budget_trims_an_oversized_tokenized_history(self):
        if budget.official_tokenizer() is None:self.skipTest('Official tokenizer is not installed.')
        history=[]
        for i in range(1600):
            history.extend([{'role':'user','content':'第%04d次：'%i+'我从东边来，已经走了许多日，见过山谷、河流和风沙。'*40},
                            {'role':'assistant','content':'我记得你说过这段路。'}])
        messages=budget.assemble_messages('角色提示','当前现场',history,'你还记得吗？',
            {'mode':'deepseek','contextWindowTokens':1_000_000})
        self.assertLess(len(messages),len(history)+3);self.assertGreater(len(messages),1000)
        self.assertNotIn('第0000次',messages[1]['content']);self.assertIn('第1599次',messages[-4]['content'])
        cost=sum(budget.message_cost(m,True) for m in messages)
        self.assertLessEqual(cost+budget.OUTPUT_RESERVE+10000,1_000_000)
        self.assertGreater(cost,980000)

    def test_long_history_crosses_the_real_local_http_gateway(self):
        captured={}
        class Model(BaseHTTPRequestHandler):
            def log_message(self,*args):pass
            def do_POST(self):
                captured.update(json.loads(self.rfile.read(int(self.headers['Content-Length']))))
                reply={'choices':[{'message':{'content':json.dumps({'speech':'我记得。','storyIntent':'continue'},ensure_ascii=False)}}]}
                raw=json.dumps(reply,ensure_ascii=False).encode()
                self.send_response(200);self.send_header('Content-Length',str(len(raw)));self.end_headers();self.wfile.write(raw)
        model=ThreadingHTTPServer(('127.0.0.1',0),Model)
        local=ThreadingHTTPServer(('127.0.0.1',0),gateway.Handler)
        for server in (model,local):threading.Thread(target=server.serve_forever,daemon=True).start()
        try:
            config=self.config(1_000_000);config['apiUrl']='http://127.0.0.1:%d/v1'%model.server_port
            payload={'npcId':'king','utterance':'我刚才说过什么？','context':{'phase':'CourtTalk'},
                     'history':self.history(5000),'connection':config}
            raw=json.dumps(payload,ensure_ascii=False).encode()
            self.assertGreater(len(raw),2_000_000);self.assertLess(len(raw),budget.REQUEST_LIMIT)
            request=urllib.request.Request('http://127.0.0.1:%d/dialogue'%local.server_port,data=raw,headers={'Content-Type':'application/json'})
            with urllib.request.urlopen(request,timeout=30) as response:result=json.load(response)
            self.assertEqual(result['mode'],'online');self.assertEqual(result['speech'],'我记得。')
            self.assertGreater(len(captured['messages']),12);self.assertEqual(captured['max_tokens'],320)
            self.assertLessEqual(sum(budget.message_cost(m,False) for m in captured['messages'])+320,1_000_000)
            self.assertIn('4999',captured['messages'][-4]['content'])
        finally:
            for server in (local,model):server.shutdown();server.server_close()


if __name__=='__main__':unittest.main()
