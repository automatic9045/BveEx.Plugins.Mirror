using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SlimDX;

namespace Automatic9045.AtsEx.Mirror
{
    internal static class MatrixExtensions
    {
        /// <summary>
        /// 指定した行列から平行移動成分を取り除いた行列を取得します。
        /// </summary>
        /// <param name="matrix">平行移動成分を取り除く行列。</param>
        /// <returns><paramref name="matrix"/> から平行移動成分を取り除いた行列。</returns>
        public static Matrix RemoveTranslation(this Matrix matrix)
        {
            Matrix result = matrix;
            result.M41 = 0;
            result.M42 = 0;
            result.M43 = 0;

            return result;
        }

        public static Vector3 ToVector3(this Vector4 vector)
            => new Vector3(vector.X, vector.Y, vector.Z);
    }
}
