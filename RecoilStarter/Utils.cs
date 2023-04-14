using System.Text;

namespace RecoilStarter
{
    public static class Utils
    {
        public static string AsHexString(this byte[] array)
        {
            if (array == null) { return ""; }
            StringBuilder hex = new StringBuilder(array.Length * 2);
            foreach (byte b in array)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
    }
}
