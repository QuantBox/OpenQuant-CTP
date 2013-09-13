using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Text.RegularExpressions;

#if CTP
using QuantBox.CSharp2CTP;

namespace QuantBox.Helper.CTP
#elif CTPZQ
using QuantBox.CSharp2CTPZQ;

namespace QuantBox.Helper.CTPZQ
#endif
{
    public static class ParseErrorType
    {
        public static ErrorType GetError(string text)
        {
            var match = Regex.Match(text, @"\|(\d+)#");
            if (match.Success)
            {
                var code = match.Groups[1].Value;
                return (ErrorType)Convert.ToInt32(code);
            }
            return (ErrorType)0;
        }
    }
}
