using System;

namespace AssetManagement.Application.DTOs
{
    public class BusinessException : Exception
    {
        public BusinessException(string message) : base(message)
        {
        }
    }
}
