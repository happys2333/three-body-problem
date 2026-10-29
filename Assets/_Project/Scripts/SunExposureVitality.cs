using System;

namespace ThreeBodyProblem
{
    // Hidden player health. Exposure events come from the physical shade query, never the dialogue model.
    public sealed class SunExposureVitality
    {
        public int Maximum { get; }
        public int Health { get; private set; }
        public bool IsDown => Health<=0;
        private readonly int damage;
        private readonly float interval;
        private float elapsed;
        private bool exposed;
        public SunExposureVitality(int maximum=100,int damagePerTick=10,float damageInterval=1)
        {Maximum=Math.Max(1,maximum);Health=Maximum;damage=Math.Max(1,damagePerTick);interval=Math.Max(.05f,damageInterval);}
        public int Tick(bool inSun,float seconds)
        {
            if(!inSun){Restore();return 0;}
            if(IsDown)return 0;
            int before=Health;
            if(!exposed){exposed=true;Health=Math.Max(0,Health-damage);}
            elapsed+=Math.Max(0,seconds);
            while(elapsed>=interval&&Health>0){elapsed-=interval;Health=Math.Max(0,Health-damage);}
            return before-Health;
        }
        public void Restore(){Health=Maximum;elapsed=0;exposed=false;}
    }
}
