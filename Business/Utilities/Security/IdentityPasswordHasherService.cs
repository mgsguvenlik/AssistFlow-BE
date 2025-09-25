using Core.Utilities.Security;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Utilities.Security
{
    public class IdentityPasswordHasherService : IPasswordHasherService
    {
        private readonly PasswordHasher<object> _passwordHasher;

        public IdentityPasswordHasherService()
        {
            _passwordHasher = new PasswordHasher<object>();
        }

        public string HashPassword(string password)
        {
            return _passwordHasher.HashPassword(null, password);
        }

        public bool VerifyPassword(string hashedPassword, string providedPassword)
        {
            try
            {
                var result = _passwordHasher.VerifyHashedPassword(null, hashedPassword, providedPassword);
                return result == PasswordVerificationResult.Success;
            }
            catch (Exception ex)
            {
                return false; // or handle the exception as needed
            }

        }
    }
}
