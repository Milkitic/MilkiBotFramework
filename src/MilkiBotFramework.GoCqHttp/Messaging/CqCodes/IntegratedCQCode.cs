using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MilkiBotFramework.GoCqHttp.Messaging.CqCodes
{
    // ReSharper disable once InconsistentNaming
    public class IntegratedCQCode : CQCode
    {
        public List<CQCode> CqCodes { get; }

        public IntegratedCQCode()
        {
            CqCodes = new List<CQCode>();
        }

        public IntegratedCQCode(IEnumerable<CQCode> coolQCodes)
        {
            CqCodes = coolQCodes.ToList();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var code in CqCodes)
                sb.Append(code + " ");
            sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }

        public override string Encode()
        {
            var sb = new StringBuilder();
            foreach (var code in CqCodes)
                sb.Append(code.Encode());
            return sb.ToString();
        }
    }
}