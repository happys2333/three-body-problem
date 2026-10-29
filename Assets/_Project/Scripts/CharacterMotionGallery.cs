using System;
using UnityEngine;

namespace ThreeBodyProblem
{
    public sealed class CharacterMotionGallery : MonoBehaviour
    {
        [Serializable] public class Entry { public string id, label, source; public AnimationClip clip; }
        public Camera viewCamera;
        public Font uiFont;
        public GameObject[] modelRoots;
        public GameObject[] platforms;
        public Animator[] animators;
        public string[] roleNames;
        public Entry[] motions;
        public string layerName = "Base Layer";

        public int SelectedIndex { get; private set; }
        public string SelectedId => motions != null && motions.Length > 0 ? motions[SelectedIndex].id : "";
        public bool Paused { get; private set; }
        private string filter = "";
        private Vector2 scroll;
        private int focusedRole = -1;
        private float speed = 1, yaw, pitch = 4, zoom = 1.8f;
        private bool typing;
        private GUIStyle heading, text, small, button, searchField;
        private float Scale => Mathf.Min(Screen.width / 1600f, Screen.height / 900f);

        private void Start()
        {
            // Paused actors need fresh skin matrices after hiding and showing their model root.
            foreach (var root in modelRoots)
                foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.updateWhenOffscreen = true;
                    skin.forceMatrixRecalculationPerRender = true;
                }
            int idle = Array.FindIndex(motions, m => m.id == "MX_Idle");
            SelectClip(Mathf.Max(0, idle));
        }

        public bool SelectClip(string id)
        {
            int index = Array.FindIndex(motions, m => m.id == id);
            if (index < 0) return false;
            SelectClip(index); return true;
        }

        public void SelectClip(int index)
        {
            if (motions == null || motions.Length == 0) return;
            SelectedIndex = (index % motions.Length + motions.Length) % motions.Length;
            foreach (var animator in animators)
            {
                if (!animator.gameObject.activeInHierarchy) continue;
                animator.Rebind(); animator.applyRootMotion = false;
                animator.Play(layerName + "." + SelectedId, 0, 0);
                animator.speed = Paused ? 0 : speed; animator.Update(0);
            }
        }

        public void SetPaused(bool value)
        {
            Paused = value;
            foreach (var animator in animators) animator.speed = Paused ? 0 : speed;
        }

        public void SetNormalizedTime(float value)
        {
            SetPaused(true);
            foreach (var animator in animators)
                if (animator.gameObject.activeInHierarchy)
                {
                    animator.Play(layerName + "." + SelectedId, 0, Mathf.Clamp01(value)); animator.Update(0);
                }
        }

        public void FocusRole(int index)
        {
            focusedRole = index; yaw = 0; pitch = 4; zoom = 1.8f;
            for (int i = 0; i < modelRoots.Length; i++) modelRoots[i].SetActive(index < 0 || index == i);
            if (platforms != null)
                for (int i = 0; i < platforms.Length; i++) platforms[i].SetActive(index < 0 || i == index / 3);
            SelectClip(SelectedIndex);
        }

        private void Update()
        {
            if (!typing)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow)) SelectClip(SelectedIndex - 1);
                if (Input.GetKeyDown(KeyCode.RightArrow)) SelectClip(SelectedIndex + 1);
                if (Input.GetKeyDown(KeyCode.Space)) SetPaused(!Paused);
                if (Input.GetKeyDown(KeyCode.R)) SelectClip(SelectedIndex);
            }
            if (focusedRole >= 0 && Input.mousePosition.x > 320 * Scale)
            {
                if (Input.GetMouseButton(1))
                {
                    yaw += Input.GetAxisRaw("Mouse X") * 3;
                    pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 3, -25, 45);
                }
                zoom = Mathf.Clamp(zoom - Input.mouseScrollDelta.y * 0.12f, 1.1f, 3f);
            }
            float left = 320 * Scale / Screen.width;
            viewCamera.rect = new Rect(left, 0, 1 - left, 1);
            var center = focusedRole < 0 ? new Vector3(0, 2.3f, 0) : modelRoots[focusedRole].transform.position + Vector3.up;
            var rotation = focusedRole < 0 ? Quaternion.Euler(2, 0, 0) : Quaternion.Euler(pitch, yaw, 0);
            viewCamera.transform.position = center + rotation * new Vector3(0, 0, 8);
            viewCamera.transform.LookAt(center); viewCamera.orthographicSize = focusedRole < 0 ? 3.15f : zoom;
        }

        private void OnGUI()
        {
            if (motions == null || motions.Length == 0) return;
            if (heading == null)
            {
                heading = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 23, wordWrap = true };
                text = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 17, wordWrap = true };
                small = new GUIStyle(text) { fontSize = 14 };
                button = new GUIStyle(GUI.skin.button) { font = uiFont, fontSize = 15, wordWrap = true };
                searchField = new GUIStyle(GUI.skin.textField) { font = uiFont, fontSize = 16 };
            }
            var originalMatrix = GUI.matrix; var originalColor = GUI.color;
            float scale = Scale, height = Screen.height / scale, width = Screen.width / scale;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUI.color = new Color(0.075f, 0.095f, 0.12f); GUI.DrawTexture(new Rect(0, 0, 320, height), Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(new Rect(16, 14, 292, 34), "人物动作预览", heading);
            GUI.Label(new Rect(16, 51, 292, 26), "六位主要人物 · " + motions.Length + " 个动作", text);
            if (GUI.Button(new Rect(16, 86, 288, 30), "查看全部人物", button)) FocusRole(-1);
            for (int i = 0; i < roleNames.Length; i++)
                if (GUI.Button(new Rect(16 + (i % 3) * 98, 122 + (i / 3) * 34, 92, 30), roleNames[i], button)) FocusRole(i);
            GUI.Label(new Rect(16, 195, 292, 22), "搜索中文名称或动作编号", small);
            GUI.SetNextControlName("MotionSearch"); filter = GUI.TextField(new Rect(16, 222, 288, 28), filter, searchField);
            typing = GUI.GetNameOfFocusedControl() == "MotionSearch";
            int visible = 0;
            foreach (var entry in motions) if (Matches(entry)) visible++;
            scroll = GUI.BeginScrollView(new Rect(12, 260, 300, height - 320), scroll, new Rect(0, 0, 278, visible * 40));
            int row = 0;
            for (int i = 0; i < motions.Length; i++)
            {
                if (!Matches(motions[i])) continue;
                GUI.backgroundColor = SelectedIndex == i ? new Color(0.86f, 0.66f, 0.3f) : Color.white;
                if (GUI.Button(new Rect(0, row++ * 40, 276, 36), new GUIContent(motions[i].label, motions[i].id), button)) SelectClip(i);
            }
            GUI.backgroundColor = Color.white; GUI.EndScrollView();
            GUI.Label(new Rect(16, height - 51, 292, 42), "左右键切换 · 空格暂停 · R 重播\n单人视图：右键旋转、滚轮缩放", small);

            var selected = motions[SelectedIndex];
            GUI.Label(new Rect(342, 16, width - 365, 34), selected.label, heading);
            GUI.Label(new Rect(342, 55, width - 365, 25), selected.source + " · " + selected.clip.length.ToString("0.00") + " 秒", small);
            float bottom = height - 90;
            if (GUI.Button(new Rect(342, bottom, 85, 30), "上一个", button)) SelectClip(SelectedIndex - 1);
            if (GUI.Button(new Rect(435, bottom, 85, 30), Paused ? "播放" : "暂停", button)) SetPaused(!Paused);
            if (GUI.Button(new Rect(528, bottom, 85, 30), "重播", button)) SelectClip(SelectedIndex);
            if (GUI.Button(new Rect(621, bottom, 85, 30), "下一个", button)) SelectClip(SelectedIndex + 1);
            GUI.Label(new Rect(725, bottom + 5, 90, 22), "速度 " + speed.ToString("0.00") + "×", small);
            float newSpeed = GUI.HorizontalSlider(new Rect(820, bottom + 10, 180, 20), speed, 0.25f, 1.5f);
            if (!Mathf.Approximately(speed, newSpeed)) { speed = newSpeed; SetPaused(Paused); }
            var active = Array.Find(animators, a => a.gameObject.activeInHierarchy);
            float progress = active == null ? 0 : active.GetCurrentAnimatorStateInfo(0).normalizedTime;
            progress = selected.clip.isLooping ? Mathf.Repeat(progress, 1) : Mathf.Clamp01(progress);
            float seek = GUI.HorizontalSlider(new Rect(342, bottom + 40, width - 365, 20), progress, 0, 1);
            if (Mathf.Abs(seek - progress) > 0.002f) SetNormalizedTime(seek);
            GUI.Label(new Rect(342, height - 24, width - 365, 22), "移动动作在原地预览；持物、书写和箱盖动作仍需对齐实际道具。", small);
            for (int i = 0; i < modelRoots.Length; i++)
                if (modelRoots[i].activeSelf)
                {
                    var point = viewCamera.WorldToScreenPoint(modelRoots[i].transform.position + Vector3.up * 2.15f);
                    GUI.Label(new Rect(point.x / scale - 45, (Screen.height - point.y) / scale, 120, 25), roleNames[i], text);
                }
            GUI.color = originalColor; GUI.matrix = originalMatrix;
        }

        private bool Matches(Entry entry) => string.IsNullOrWhiteSpace(filter) ||
            entry.label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || entry.id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
