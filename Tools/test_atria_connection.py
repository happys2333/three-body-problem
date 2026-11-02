"""Atria chat profile regression; mocked transport, no secrets or paid calls."""
import io
import json
import unittest
import llm_connection as llm

class AtriaConnectionTests(unittest.TestCase):
    def test_http_200_content_rejection_is_not_a_successful_model_answer(self):
        class Rejection(io.BytesIO):
            headers={'Content-Type':'text/event-stream'}
        def reject(_request,timeout):
            return Rejection(b'data: {"error":{"message":"private service details", "code":"content_check_rejected","type":"content_check_error"}}\n\n')
        config={'mode':'custom','apiUrl':'https://discovery-api.intern-ai.org.cn/v1','apiKey':'placeholder-only','model':'Atria-Dawn','timeoutSeconds':20}
        result=llm.connection_test(config,opener=reject)
        self.assertFalse(result['ok'])
        self.assertIn('服务商拒绝',result['message'])
        self.assertNotIn('private service details',str(result))

    def test_intern_atria_disables_reasoning_within_the_existing_npc_output_budget(self):
        captured={}
        def respond(request,timeout):
            captured.update(json.loads(request.data))
            return io.BytesIO(json.dumps({'choices':[{'message':{'content':'{"speech":"先到岩下。"}'},'finish_reason':'stop'}]}).encode())
        config={'mode':'custom','apiUrl':'https://discovery-api.intern-ai.org.cn/v1','apiKey':'placeholder-only','model':'Atria-Dawn','timeoutSeconds':20}
        result=llm.chat_completion(llm.resolve_connection(config),[{'role':'user','content':'这里安全吗？'}],opener=respond)
        self.assertEqual(result['speech'],'先到岩下。')
        self.assertEqual(captured.get('thinking'),{'type':'disabled'})
        self.assertEqual(captured['max_tokens'],320)
        self.assertNotIn('placeholder-only',json.dumps(captured))

    def test_other_custom_services_do_not_receive_unsupported_provider_parameters(self):
        captured={}
        def respond(request,timeout):
            captured.update(json.loads(request.data));return io.BytesIO(b'{"choices":[{"message":{"content":"{\\"ok\\":true}"}}]}')
        config={'mode':'custom','apiUrl':'https://example.test/v1','apiKey':'placeholder-only','model':'other-model','timeoutSeconds':20}
        self.assertTrue(llm.chat_completion(llm.resolve_connection(config),[{'role':'user','content':'hello'}],opener=respond)['ok'])
        self.assertNotIn('thinking',captured)

if __name__=='__main__':unittest.main()
