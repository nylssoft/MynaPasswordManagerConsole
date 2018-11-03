/*
    Myna Password Manager Console
    Copyright (C) 2018 Niels Stockfleth

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace PasswordManagerConsole
{
    public static class SecureStringExtension
    {

        public static string GetAsString(this SecureString securePassword)
        {
            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(securePassword);
                return Marshal.PtrToStringUni(unmanagedString);
            }
            finally
            {
                if (unmanagedString != IntPtr.Zero)
                {
                    Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
                }
            }
        }

        public static bool IsEqualTo(this SecureString ss1, SecureString ss2)
        {
            IntPtr bstr1 = IntPtr.Zero;
            IntPtr bstr2 = IntPtr.Zero;
            try
            {
                bstr1 = Marshal.SecureStringToBSTR(ss1);
                bstr2 = Marshal.SecureStringToBSTR(ss2);
                int length1 = Marshal.ReadInt32(bstr1, -4);
                int length2 = Marshal.ReadInt32(bstr2, -4);
                if (length1 != length2)
                {
                    return false;
                }
                for (int x = 0; x < length1; ++x)
                {
                    byte b1 = Marshal.ReadByte(bstr1, x);
                    byte b2 = Marshal.ReadByte(bstr2, x);
                    if (b1 != b2)
                    {
                        return false;
                    }
                }
                return true;
            }
            finally
            {
                if (bstr2 != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(bstr2);
                }
                if (bstr1 != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(bstr1);
                }
            }
        }
    }
}
