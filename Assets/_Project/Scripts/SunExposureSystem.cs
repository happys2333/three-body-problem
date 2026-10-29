using System.Collections;
using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(TravellerStage))]
    public sealed class SunExposureSystem : MonoBehaviour
    {
        public GobiStorySettings settings;
        public SunExposureVitality Vitality { get; private set; }
        public bool PlayerIsSheltered { get; private set; }
        public bool IsRecovering { get; private set; }
        public float HitPulse { get; private set; }
        public float Blackout { get; private set; }
        public int DamageEvents { get; private set; }
        private TravellerStage stage;private TravellerHud hud;private GobiSequenceController sequence;
        private TravellerActor[] actors;private SkinnedMeshRenderer[][] skins;private Color[][] colours;
        private MaterialPropertyBlock tint;
        private static readonly RaycastHit[] ShadeHits=new RaycastHit[48];
        private float nextShadeCheck,nextNpcCheck;private bool exposed;
        private bool npcTinted;

        private void Start()
        {
            tint=new MaterialPropertyBlock();
            stage=GetComponent<TravellerStage>();hud=GetComponentInChildren<TravellerHud>();sequence=GetComponent<GobiSequenceController>();
            Vitality=new SunExposureVitality(settings.maximumHealth,settings.sunlightDamage,settings.sunlightDamageInterval);
            actors=GetComponentsInChildren<TravellerActor>();skins=new SkinnedMeshRenderer[actors.Length][];colours=new Color[actors.Length][];
            for(int i=0;i<actors.Length;i++)
            {
                skins[i]=actors[i].GetComponentsInChildren<SkinnedMeshRenderer>();colours[i]=new Color[skins[i].Length];
                for(int j=0;j<skins[i].Length;j++){var material=skins[i][j].sharedMaterial;colours[i][j]=material!=null&&material.HasProperty("_Color")?material.color:Color.white;}
            }
        }

        public static bool IsSheltered(Vector3 torso,Vector3 towardSun,Transform ignore)
        {
            if(towardSun.y<=0)return true;
            return Blocked(torso,towardSun,ignore)&&Blocked(torso+Vector3.up*.5f,towardSun,ignore);
        }
        private static bool Blocked(Vector3 point,Vector3 direction,Transform ignore)
        {
            int count=Physics.RaycastNonAlloc(point,direction.normalized,ShadeHits,2000,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var hit=ShadeHits[i];if(ignore!=null&&hit.transform.IsChildOf(ignore))continue;
                if(hit.collider is CharacterController)continue;
                return true;
            }
            return false;
        }

        private void Update()
        {
            if(stage==null||Vitality==null)return;
            HitPulse=Mathf.MoveTowards(HitPulse,0,Time.unscaledDeltaTime*2.2f);
            if(hud!=null&&hud.IsWorldPaused&&!stage.Journey.IsTitle)return;
            bool active=stage.Journey!=null&&!stage.Journey.IsTitle&&!stage.Journey.IsPreviewing&&!stage.IsLoading&&(hud==null||!hud.IsWorldPaused)&&stage.Current.heat>.5f&&(sequence==null||sequence.SunHasRisen);
            if(!active)
            {
                exposed=false;Vitality.Restore();PlayerIsSheltered=true;
                if(npcTinted){RestoreNpcTint();npcTinted=false;}
                return;
            }
            if(IsRecovering)return;
            if(Time.time>=nextShadeCheck)
            {
                nextShadeCheck=Time.time+.1f;
                PlayerIsSheltered=IsSheltered(stage.player.transform.position+Vector3.up*1.05f,stage.Current.sunA,stage.player.transform);
                exposed=!PlayerIsSheltered;
            }
            if(Vitality.Tick(exposed,Time.deltaTime)>0){DamageEvents++;HitPulse=1;}
            if(Time.time>=nextNpcCheck)
            {
                nextNpcCheck=Time.time+.2f;
                npcTinted=true;
                for(int i=0;i<actors.Length;i++)
                {
                    if(actors[i].role==TravellerRole.PlayerTraveller)continue;
                    bool hot=!IsSheltered(actors[i].transform.position+Vector3.up*1.05f,stage.Current.sunA,actors[i].transform);
                    for(int j=0;j<skins[i].Length;j++)
                    {
                        skins[i][j].GetPropertyBlock(tint);tint.SetColor("_Color",hot?Color.Lerp(colours[i][j],new Color(1,.20f,.07f),.3f+Mathf.Sin(Time.time*9)*.08f):colours[i][j]);skins[i][j].SetPropertyBlock(tint);
                    }
                }
            }
            if(Vitality.IsDown)StartCoroutine(Recover());
        }
        private void RestoreNpcTint()
        {
            for(int i=0;i<skins.Length;i++)for(int j=0;j<skins[i].Length;j++)
            {skins[i][j].GetPropertyBlock(tint);tint.SetColor("_Color",colours[i][j]);skins[i][j].SetPropertyBlock(tint);}
        }
        private IEnumerator Recover()
        {
            IsRecovering=true;stage.Journey.CloseDialogue();
            float seconds=0;
            while(seconds<settings.recoveryFadeSeconds){seconds+=Time.unscaledDeltaTime;Blackout=Mathf.Clamp01(seconds/.35f);yield return null;}
            Vector3 safe=stage.shelterCenter!=null?stage.shelterCenter.position:stage.player.transform.position;
            stage.player.Teleport(safe+Vector3.up*stage.player.eyeHeight,safe+Vector3.up*stage.player.eyeHeight+Vector3.forward*20);
            Vitality.Restore();exposed=false;PlayerIsSheltered=true;HitPulse=0;
            seconds=0;while(seconds<.5f){seconds+=Time.unscaledDeltaTime;Blackout=1-Mathf.Clamp01(seconds/.5f);yield return null;}
            Blackout=0;IsRecovering=false;stage.Journey.Speak("旅人：别再站到日光里。先在这里缓一缓。",6);
        }
    }
}
