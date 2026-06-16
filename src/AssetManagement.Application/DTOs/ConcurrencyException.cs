using System;

namespace AssetManagement.Application.DTOs
{
    public class ConcurrencyException : BusinessException
    {
        public ConcurrencyException(string message)
            : base(message)
        {
        }
    }
}
