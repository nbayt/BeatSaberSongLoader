using System;
using System.Threading;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace SongLoaderPlugin
{
    public class DifficultyDisplay : MonoBehaviour
    {
        TextMeshPro _timeMesh;

        void Awake()
        {
            _timeMesh = this.gameObject.AddComponent<TextMeshPro>();
            _timeMesh.text = "";
            _timeMesh.fontSize = 3;
            _timeMesh.color = Color.white;
            _timeMesh.font = Resources.Load<TMP_FontAsset>("Teko-Medium SDF No Glow");
            _timeMesh.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1.5f);
            _timeMesh.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1.5f);
            _timeMesh.rectTransform.position = new Vector3(1.85f, -0.75f, 3.00f);
            _timeMesh.rectTransform.Rotate(new Vector3(22.5f, 0.0f, 0.0f));
        }

        void FixedUpdate()
        {
            SongDetailViewController SDVC = Resources.FindObjectsOfTypeAll<SongDetailViewController>().FirstOrDefault();
            DifficultyViewController DVC = Resources.FindObjectsOfTypeAll<DifficultyViewController>().FirstOrDefault();
            if (SDVC != null)
            {
                var diff_selected = ReflectionUtil.GetPrivateField<LevelStaticData.Difficulty>(SDVC, "_difficulty");
                var diff_info = ReflectionUtil.GetPrivateField<LevelStaticData>(SDVC, "_levelStaticData");
                var diff = diff_info.GetDifficultyLevel(diff_selected);
                _timeMesh.text = "Diff: "+(float)(diff.difficultyRank)/100.0f; // can only store ranks as int so we mult by 100 to keep two decimal points, bring them back here
            }
        }
    }
}