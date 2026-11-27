using LoanDau.Data;
using UnityEngine;

namespace LoanDau.Art
{
    public enum StageMarkerKind { PlayerSpawn, PropSpawn, HazardPath }

    /// <summary>Marker trong stage_*.fbx (spawn_p1/p2, spawn_prop_xx, hazard_*). Gắn lúc import.</summary>
    public class StageMarker : MonoBehaviour
    {
        public StageMarkerKind kind;
        [Tooltip("PlayerSpawn: 1 hoặc 2")] public int playerIndex;
        [Tooltip("PropSpawn: cỡ tối đa của vật được sinh ở đây")] public PropSize propSize;
        [Tooltip("PropSpawn: tên model mặc định, vd prop_ghe_nhua")] public string defaultProp;
        [Tooltip("HazardPath: chu kỳ (giây)")] public float intervalSeconds;

        void OnDrawGizmos()
        {
            Gizmos.color = kind switch
            {
                StageMarkerKind.PlayerSpawn => playerIndex == 1 ? Color.red : Color.blue,
                StageMarkerKind.PropSpawn => Color.yellow,
                _ => Color.magenta,
            };
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.25f, Vector3.one * 0.5f);
        }
    }
}
