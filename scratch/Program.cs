using System;
using BCrypt.Net;

var hash = BCrypt.Net.BCrypt.HashPassword("LoanFlow@2026!");
Console.WriteLine(hash);
