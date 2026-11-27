using UnityEngine;

namespace LoanDau.EditorTools
{
    /// <summary>
    /// Đổi toạ độ Blender → Unity cho FBX xuất với Forward -Z, Up Y, Apply Transform (Asset Bible mục 1).
    ///   Blender (x, y, z) → FBX (x, z, -y) → Unity (-x, z, -y)   [Unity đảo trục X khi đọc FBX]
    /// Kiểm tra: mặt nhân vật Blender -Y → Unity +Z (forward); đầu đánh Blender +Z → Unity +Y.
    /// </summary>
    public static class BlenderSpace
    {
        public static Vector3 PointToUnity(Vector3 b) => new(-b.x, b.z, -b.y);

        /// <summary>Euler 'XYZ' của Blender (độ) → Quaternion Unity, qua ma trận: R_u = C · R_b · Cᵀ.</summary>
        public static Quaternion EulerXYZDegToUnity(Vector3 eulerDeg)
        {
            double a = eulerDeg.x * Mathf.Deg2Rad, b = eulerDeg.y * Mathf.Deg2Rad, c = eulerDeg.z * Mathf.Deg2Rad;
            double ca = System.Math.Cos(a), sa = System.Math.Sin(a);
            double cb = System.Math.Cos(b), sb = System.Math.Sin(b);
            double cc = System.Math.Cos(c), sc = System.Math.Sin(c);
            // Blender XYZ euler: R = Rz(c) · Ry(b) · Rx(a)
            var rb = new double[3, 3]
            {
                { cc * cb, cc * sb * sa - sc * ca, cc * sb * ca + sc * sa },
                { sc * cb, sc * sb * sa + cc * ca, sc * sb * ca - cc * sa },
                { -sb,     cb * sa,                cb * ca },
            };
            // Áp R_u lên trục cơ sở của Unity: R_u·e = C·R_b·(Cᵀ·e)
            Vector3 Apply(Vector3 u)
            {
                var bv = new double[] { -u.x, -u.z, u.y };          // Cᵀ·u  (Unity → Blender)
                var r = new double[3];
                for (int i = 0; i < 3; i++) r[i] = rb[i, 0] * bv[0] + rb[i, 1] * bv[1] + rb[i, 2] * bv[2];
                return PointToUnity(new Vector3((float)r[0], (float)r[1], (float)r[2]));
            }
            return Quaternion.LookRotation(Apply(Vector3.forward), Apply(Vector3.up));
        }
    }
}
