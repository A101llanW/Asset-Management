using System.Collections.Generic;
using System.Linq;

namespace AssetManagement.Application.Contracts
{
    public class EmailChangeResult
    {
        public bool Succeeded { get; set; }

        public IEnumerable<string> Errors { get; set; }

        public static EmailChangeResult Success()
        {
            return new EmailChangeResult { Succeeded = true, Errors = new string[0] };
        }

        public static EmailChangeResult Failure(params string[] errors)
        {
            return new EmailChangeResult
            {
                Succeeded = false,
                Errors = errors == null ? new string[0] : errors.Where(e => !string.IsNullOrWhiteSpace(e)).ToArray()
            };
        }

        public static EmailChangeResult Failure(IEnumerable<string> errors)
        {
            return Failure(errors == null ? null : errors.ToArray());
        }
    }
}
