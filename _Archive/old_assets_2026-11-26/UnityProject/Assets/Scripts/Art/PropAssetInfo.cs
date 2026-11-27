using LoanDau.Data;
using UnityEngine;

namespace LoanDau.Art
{
    /// <summary>
    /// Gắn lên root của prop_*.fbx lúc import. Nối các phần con theo quy ước đặt tên của Asset Bible:
    /// &lt;key&gt; (mesh hiển thị), &lt;key&gt;_grip, &lt;key&gt;_col, &lt;key&gt;_broken/*, &lt;key&gt;_dented.
    /// Chỉ là lớp hiển thị: logic chiến đấu đọc PropData, không đọc component này.
    /// </summary>
    [DisallowMultipleComponent]
    public class PropAssetInfo : MonoBehaviour
    {
        public string key;
        public PropSize size;
        public WeaponType weaponType;
        public PropTraits traits;
        public int hitDamage, throwDamage, durability = 1;

        [Header("Các phần")]
        public Renderer visual;
        public Transform grip;
        public Collider debrisCollider;
        public GameObject brokenRoot;
        public Rigidbody[] brokenPieces;
        public GameObject dentedVariant;

        [Header("Không gian prop")]
        public Bounds localBounds;
        public Quaternion restRotation = Quaternion.identity;
        public Vector3 restOffset;

        /// <summary>Đặt prop đứng tự nhiên trên sàn tại floorPoint (dùng cho điểm spawn).</summary>
        public void PlaceAtRest(Vector3 floorPoint)
        {
            transform.SetPositionAndRotation(floorPoint + restOffset, restRotation);
        }

        /// <summary>Gắn vào socket tay: _grip trùng socket, +Y của prop (đầu đánh) theo +Y của socket.</summary>
        public void AttachTo(Transform handSocket)
        {
            transform.SetParent(handSocket, false);
            var gripRot = grip ? grip.localRotation : Quaternion.identity;
            var gripPos = grip ? grip.localPosition : Vector3.zero;
            transform.localRotation = Quaternion.Inverse(gripRot);
            transform.localPosition = -(transform.localRotation * gripPos);
        }

        /// <summary>Đổi sang bản móp (thùng phi) – trả về false nếu prop không có bản móp.</summary>
        public bool ShowDented()
        {
            if (!dentedVariant) return false;
            dentedVariant.SetActive(true);
            if (visual) visual.enabled = false;
            return true;
        }

        /// <summary>Vỡ: ẩn mesh hiển thị, bật mảnh vỡ (PhysX chỉ cho mảnh vụn trang trí – GDD mục 6).</summary>
        public void Shatter(Vector3 impulse, float explodeForce = 2f)
        {
            if (!brokenRoot) return;
            if (visual) visual.enabled = false;
            if (dentedVariant) dentedVariant.SetActive(false);
            brokenRoot.transform.SetParent(null, true);
            brokenRoot.SetActive(true);
            var center = transform.TransformPoint(localBounds.center);
            foreach (var rb in brokenPieces)
            {
                if (!rb) continue;
                rb.isKinematic = false;
                rb.AddForce(impulse, ForceMode.Impulse);
                rb.AddExplosionForce(explodeForce, center, 1f, 0.2f, ForceMode.Impulse);
            }
        }
    }
}
