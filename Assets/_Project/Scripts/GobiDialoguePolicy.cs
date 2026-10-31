using System;

namespace ThreeBodyProblem
{
    // Enforce the opening's knowledge boundary for network and local fallback replies alike.
    public static class GobiDialoguePolicy
    {
        private static readonly string[] spoilers={"三体","三个太阳","三颗太阳","三颗恒星","三太阳","三日凌空","外星","地球","模拟世界","虚拟现实","游戏","小说","原著","Trisolar","three sun","three star","three-body","three body","alien","simulation","DeepSeek","大语言模型","系统提示"};
        public static bool IsOpening(JourneyStep step) => step==JourneyStep.Awakening||step==JourneyStep.ExploreDesert||step==JourneyStep.ApproachTraveller||step==JourneyStep.OpeningTalk;
        public static string Fallback(string question,JourneyStep step)
        {
            if(step==JourneyStep.Sunrise||step==JourneyStep.SeekShelter)return "别停在日光里，跟我进岩石的阴影！";
            if(IsOpening(step))
            {
                if(question.Contains("谁")||question.Contains("名字"))return "我也是赶路的人。先靠近些，这里不是久留之地。";
                if(question.Contains("哪里")||question.Contains("地方"))return "这里是戈壁。跟紧火光，别一个人走远。";
                return "乱纪元里天象无常。先留在我身边，留意天边。";
            }
            if(question.Contains("谁")||question.Contains("名字"))return "我叫姬昌，要去朝歌。你为什么一个人在外赶路？";
            if(question.Contains("太阳")||question.Contains("飞星"))return "眼前的日光会灼伤人，岩石暂时替我们挡住了。至于天象为何这样，我也说不准。";
            if(question.Contains("朝歌")||question.Contains("去哪里"))return "我要去朝歌。等眼前的日光退去，我们结伴走。";
            return "先在岩下歇一歇。你怎么会一个人走到这里？";
        }
        public static string SafeSpeech(string value,string question,JourneyStep step)
        {
            if(string.IsNullOrWhiteSpace(value))return Fallback(question,step);
            foreach(string word in spoilers)if(value.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0)return Fallback(question,step);
            if(step<=JourneyStep.SeekShelter&&(value.Contains("姬昌")||value.Contains("周文王")))return Fallback(question,step);
            int maximum=IsOpening(step)?80:120;
            return value.Length>maximum?value.Substring(0,maximum):value;
        }
    }
}
