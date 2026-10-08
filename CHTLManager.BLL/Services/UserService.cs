using System;
using System.Linq;
using CHTLManager.DAL;
using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL.Services
{
    public class UserService
    {
        public UserService()
        {
        }

        public User? Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            using var context = new AppDbContext();

            // SQL Server collation may be case-insensitive, so verify exact values in .NET.
            return context.Users
                .Where(user => user.IsActive && user.Username == username)
                .AsEnumerable()
                .FirstOrDefault(user =>
                    string.Equals(user.Username, username, StringComparison.Ordinal) &&
                    string.Equals(user.Password, password, StringComparison.Ordinal));
        }
    }
}
