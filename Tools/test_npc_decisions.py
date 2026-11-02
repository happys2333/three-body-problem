"""Actual gateway decisions and provider failures, with an isolated fake transport."""
import io
import json
import socket
import unittest
import urllib.error
import dialogue_gateway as gateway


class DecisionTests(unittest.TestCase):
    def payload(self, npc='king', phase='CourtTalk', state='calm', **changes):
        context={'phase':phase,'decisionProtocol':1,'decisionRevision':0,'authorityState':state,
                 'audienceReady':True,'guardsAvailable':True,
                 'allowedStoryIntents':['continue','advance','warn','pursue','attack','stand_down'],**changes}
        return {'npcId':npc,'utterance':'请听我说。','history':[],'context':context,
                'connection':{'mode':'custom','apiUrl':'https://example.test/v1','model':'test','apiKey':'not-a-real-key','timeoutSeconds':20}}
    def reply(self,payload,**value):
        content={'speech':'我听到了。','gesture':'talk','storyIntent':'continue','conduct':'neutral',**value}
        return gateway.dialogue(payload,opener=lambda *_a,**_k:io.BytesIO(json.dumps({'choices':[{'message':{'content':json.dumps(content)}}]}).encode()))
    def test_initial_threat_can_warn_but_never_pursue(self):
        p=self.payload()
        self.assertEqual(self.reply(p,storyIntent='pursue',conduct='violent_threat')['storyIntent'],'continue')
        self.assertEqual(self.reply(p,storyIntent='warn',conduct='violent_threat')['storyIntent'],'warn')
    def test_arrest_and_lethal_force_are_separate_explicit_orders(self):
        self.assertEqual(self.reply(self.payload(),storyIntent='attack',conduct='violent_threat')['storyIntent'],'continue')
        self.assertEqual(self.reply(self.payload(state='warned'),storyIntent='attack',conduct='violent_threat')['storyIntent'],'attack')
        self.assertEqual(self.reply(self.payload(state='pursuing'),storyIntent='attack',conduct='violent_threat')['storyIntent'],'attack')
        self.assertEqual(self.reply(self.payload(state='warned'),storyIntent='attack',conduct='question')['storyIntent'],'continue')
    def test_prior_warning_and_actual_defiance_are_both_required(self):
        p=self.payload(state='warned')
        self.assertEqual(self.reply(p,storyIntent='pursue',conduct='persistent_defiance')['storyIntent'],'pursue')
        for conduct in ['neutral','clarification','relevant_proposal','agreement','apology',{},None]:
            with self.subTest(conduct=conduct):self.assertEqual(self.reply(p,storyIntent='pursue',conduct=conduct)['storyIntent'],'continue')
        p['context']['guardsAvailable']=False
        self.assertEqual(self.reply(p,storyIntent='pursue',conduct='persistent_defiance')['storyIntent'],'continue')
    def test_chatter_is_not_an_explanation_or_task_completion(self):
        for conduct in ['neutral','question','clarification','disruption','violent_threat','apology']:
            with self.subTest(conduct=conduct):self.assertEqual(self.reply(self.payload(),storyIntent='advance',conduct=conduct)['storyIntent'],'continue')
        self.assertEqual(self.reply(self.payload(),storyIntent='advance',conduct='relevant_proposal')['storyIntent'],'advance')
        self.assertEqual(self.reply(self.payload(audienceReady=False),storyIntent='advance',conduct='agreement')['storyIntent'],'continue')
    def test_current_task_speaker_and_advertised_capabilities_are_required(self):
        for npc,phase in [('citizen','CourtTalk'),('wenwang','CourtTalk'),('king','ShelterTalk'),('pope','CourtTalk')]:
            self.assertEqual(self.reply(self.payload(npc,phase),storyIntent='advance',conduct='agreement')['storyIntent'],'continue')
        for npc in ['pope','scholar','davinci']:
            self.assertEqual(self.reply(self.payload(npc,'TribunalTalk'),storyIntent='advance',conduct='agreement')['storyIntent'],'advance')
        self.assertEqual(self.reply(self.payload(allowedStoryIntents=['continue']),storyIntent='advance',conduct='agreement')['storyIntent'],'continue')
    def test_only_authority_can_recall_guards_and_cannot_do_so_for_new_threats(self):
        self.assertEqual(self.reply(self.payload(state='pursuing'),storyIntent='stand_down',conduct='apology')['storyIntent'],'stand_down')
        for npc in ['shang_guard','citizen','scholar']:
            self.assertEqual(self.reply(self.payload(npc,state='pursuing'),storyIntent='stand_down',conduct='apology')['storyIntent'],'continue')
        self.assertEqual(self.reply(self.payload(state='pursuing'),storyIntent='stand_down',conduct='violent_threat')['storyIntent'],'continue')
    def test_filtered_speech_cannot_keep_a_hidden_execution_decision(self):
        reply=self.reply(self.payload(state='warned'),speech='这不是三体小说。',storyIntent='pursue',conduct='persistent_defiance',health=0,killPlayer=True)
        self.assertEqual(reply['storyIntent'],'continue');self.assertEqual(reply['conduct'],'neutral');self.assertNotIn('health',reply)
    def test_provider_errors_are_visible_and_never_advance_even_the_sky(self):
        errors=[(TimeoutError('not-a-real-key'),'timeout'),(urllib.error.URLError(socket.timeout()),'timeout'),
                (urllib.error.HTTPError('private',401,'not-a-real-key',{},None),'authentication'),
                (urllib.error.HTTPError('private',429,'not-a-real-key',{},None),'rate_limited')]
        for error,code in errors:
            def fail(*_a,**_k):raise error
            p=self.payload('wenwang','OpeningTalk',openingAnswers=2,allowedStoryIntents=['continue','sunrise'])
            reply=gateway.dialogue(p,opener=fail)
            self.assertEqual(reply['failureCode'],code);self.assertEqual(reply['mode'],'scripted');self.assertEqual(reply['storyIntent'],'continue')
            self.assertNotIn('not-a-real-key',json.dumps(reply))
    def test_http_200_moderation_error_is_a_failure_not_an_npc_judgment(self):
        body=b'data: {"error":{"code":"content_check_rejected","message":"private upstream detail"}}\n\n'
        reply=gateway.dialogue(self.payload(state='warned'),opener=lambda *_a,**_k:io.BytesIO(body))
        self.assertEqual(reply['failureCode'],'content_rejected');self.assertEqual(reply['storyIntent'],'continue');self.assertEqual(reply['conduct'],'neutral')
    def test_explicit_offline_mode_has_a_distinct_non_failure_marker(self):
        p=self.payload();p['connection']={'mode':'scripted'};reply=gateway.dialogue(p)
        self.assertEqual(reply['failureCode'],'offline');self.assertEqual(reply['storyIntent'],'continue')
    def test_poisoned_or_absent_speech_is_not_voiced(self):
        for speech in [None,{},[],'']:
            reply=self.reply(self.payload(),speech=speech,storyIntent='advance',conduct='agreement')
            self.assertEqual(reply['failureCode'],'invalid_response');self.assertEqual(reply['storyIntent'],'continue')

if __name__=='__main__':unittest.main()
