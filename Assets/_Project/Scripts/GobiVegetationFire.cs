using System.Collections.Generic;
using UnityEngine;

namespace ThreeBodyProblem
{
    public sealed class GobiVegetationFire : MonoBehaviour
    {
        public GobiStorySettings settings;
        public int BurnedCount {get;private set;}
        public int IgnitedCount {get;private set;}
        public int ShadedCount {get;private set;}
        private TravellerStage stage;private TravellerHud hud;
        private VegetationFuel[] fuels;
        private readonly Queue<ParticleSystem> firePool=new Queue<ParticleSystem>();
        private readonly List<ParticleSystem> allFire=new List<ParticleSystem>();
        private MaterialPropertyBlock block;
        private float clock,nextUpdate;

        private void Start()
        {
            block=new MaterialPropertyBlock();
            stage=GetComponent<TravellerStage>();hud=GetComponentInChildren<TravellerHud>();fuels=GetComponentsInChildren<VegetationFuel>();
            foreach(var fuel in fuels)
            {
                fuel.originalColours=new Color[fuel.foliage.Length];
                for(int i=0;i<fuel.foliage.Length;i++){var m=fuel.foliage[i].sharedMaterial;fuel.originalColours[i]=m!=null&&m.HasProperty("_Color")?m.color:Color.white;}
                fuel.ashes=Instantiate(settings.ashPrefab,transform);fuel.ashes.name="Ashes / "+fuel.name;
                fuel.ashes.transform.position=fuel.transform.position+Vector3.up*.025f;
                float width=fuel.foliage.Length>0?fuel.foliage[0].bounds.size.x:1;
                fuel.ashes.transform.localScale=Vector3.one*Mathf.Clamp(width*.75f,.35f,2.5f);fuel.ashes.SetActive(false);
            }
            for(int i=0;i<32;i++)
            {var go=Instantiate(settings.vegetationFirePrefab,transform);go.SetActive(false);var effect=go.GetComponent<ParticleSystem>();firePool.Enqueue(effect);allFire.Add(effect);}
        }
        public void ResetVegetation()
        {
            if(fuels==null)return;
            BurnedCount=0;IgnitedCount=0;ShadedCount=0;clock=0;firePool.Clear();
            foreach(var fire in allFire){fire.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);fire.gameObject.SetActive(false);firePool.Enqueue(fire);}
            foreach(var fuel in fuels)
            {
                fuel.phase=0;fuel.ignitionAt=-1;fuel.fire=null;if(fuel.ashes!=null)fuel.ashes.SetActive(false);
                for(int i=0;i<fuel.foliage.Length;i++){fuel.foliage[i].enabled=true;fuel.foliage[i].GetPropertyBlock(block);block.SetColor("_Color",fuel.originalColours[i]);fuel.foliage[i].SetPropertyBlock(block);}
                foreach(var solid in fuel.solids)solid.enabled=true;
            }
        }
        public void IgniteSunlit()
        {
            if(fuels==null)return;
            // Reserve visible fire emitters for the closest plants; all exposed fuel still burns to ash.
            System.Array.Sort(fuels,(a,b)=>(a.transform.position-stage.player.transform.position).sqrMagnitude.CompareTo((b.transform.position-stage.player.transform.position).sqrMagnitude));
            for(int i=0;i<fuels.Length;i++)
            {
                var fuel=fuels[i];Vector3 point=fuel.foliage.Length>0?fuel.foliage[0].bounds.center:fuel.transform.position+Vector3.up*.4f;
                if(SunExposureSystem.IsSheltered(point,stage.Current.sunA,fuel.transform)){ShadedCount++;continue;}
                IgnitedCount++;fuel.ignitionAt=clock+(i%7)*.37f;
            }
        }
        private void Update()
        {
            if(fuels==null||stage.Journey==null||stage.Journey.IsTitle||stage.Journey.IsPreviewing||stage.IsLoading||(hud!=null&&hud.IsWorldPaused))return;
            clock+=Time.deltaTime;if(clock<nextUpdate)return;nextUpdate=clock+.16f;
            foreach(var fuel in fuels)
            {
                if(fuel.ignitionAt<0||fuel.phase==2||clock<fuel.ignitionAt)continue;
                if(fuel.phase==0)
                {
                    fuel.phase=1;
                    if(firePool.Count>0)
                    {
                        fuel.fire=firePool.Dequeue();fuel.fire.transform.position=fuel.transform.position+Vector3.up*.16f;
                        fuel.fire.gameObject.SetActive(true);fuel.fire.Play(true);
                    }
                }
                float amount=Mathf.Clamp01((clock-fuel.ignitionAt)/settings.burnSeconds);
                for(int i=0;i<fuel.foliage.Length;i++)
                {fuel.foliage[i].GetPropertyBlock(block);block.SetColor("_Color",Color.Lerp(fuel.originalColours[i],new Color(.045f,.035f,.027f),amount));fuel.foliage[i].SetPropertyBlock(block);}
                if(amount>=1)
                {
                    fuel.phase=2;BurnedCount++;foreach(var renderer in fuel.foliage)renderer.enabled=false;foreach(var solid in fuel.solids)solid.enabled=false;fuel.ashes.SetActive(true);
                    if(fuel.fire!=null){fuel.fire.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);fuel.fire.gameObject.SetActive(false);firePool.Enqueue(fuel.fire);fuel.fire=null;}
                }
            }
        }
    }
}
