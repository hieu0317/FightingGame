using System.Collections.Generic;
using UnityEngine;

namespace LoanDau.Art
{
    /// <summary>
    /// Custom property Blender ghi trên một object trong FBX (vd prop_size, rest_euler_blender_deg).
    /// Do ArtAssetPostprocessor gắn lúc import; chỉ để đọc.
    /// </summary>
    [DisallowMultipleComponent]
    public class FbxUserProperties : MonoBehaviour
    {
        [SerializeField] List<string> keys = new();
        [SerializeField] List<string> values = new();

        public IReadOnlyList<string> Keys => keys;

        public void Set(string key, string value)
        {
            int i = keys.IndexOf(key);
            if (i >= 0) values[i] = value;
            else { keys.Add(key); values.Add(value); }
        }

        public bool TryGet(string key, out string value)
        {
            int i = keys.IndexOf(key);
            value = i >= 0 ? values[i] : null;
            return i >= 0;
        }

        public string Get(string key, string fallback = "") => TryGet(key, out var v) ? v : fallback;

        public int GetInt(string key, int fallback = 0) =>
            TryGet(key, out var v) && float.TryParse(v, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var f) ? Mathf.RoundToInt(f) : fallback;

        public float GetFloat(string key, float fallback = 0f) =>
            TryGet(key, out var v) && float.TryParse(v, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : fallback;
    }
}
