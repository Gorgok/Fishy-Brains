using System;
using Microsoft.SPOT;

namespace WCR.CONVERT
{
    /**********************************************************************
    // Routine : Motor class
    //---------------------------------------------------------------------
    // Date : 20110516
    // Name : Wim Cranen
    //---------------------------------------------------------------------
    // Version : v1.0
    //---------------------------------------------------------------------
    // History : v1.0 Byte_to_hex and Byte_to_bin routines
    //         : 
    //         : 
    //           
    **********************************************************************/
    class c_Convert
    {
        // For debug purposes
        bool b_Debug = true;
        // Convert a four bytes integer into a four charachter string
        public string FourByte_ToHex(int number)
        {
            string hex = "0123456789ABCDEF";
            return new string(new char[] { hex[(number & 0xF000) >> 12], hex[(number & 0xF00) >> 8], hex[(number & 0xF0) >> 4], hex[number & 0x0F] });
        }
        // Convert a two bytes integer into a two charachter string
        public string TwoByte_ToHex(int number)
        {
            string hex = "0123456789ABCDEF";
            return new string(new char[] { hex[(number & 0xF0) >> 4], hex[number & 0x0F] });
        }
        // Convert a one byte integer into a one charachter string
        public string OneByte_ToHex(int number)
        {
            string hex = "0123456789ABCDEF";
            return new string(new char[] { hex[number & 0x0F] });
        }
        // Convert a two byte integer into a eight digit binary MSB first
        public string TwoByte_ToBinMSBF(int input)
        {
            char[] result = new char[8];
            for (int i = 7; i > -1; i--)
            {
                if ((input & 0x1) == 1) { result[i] = '1'; }
                else { result[i] = '0'; }
                input >>= 1;
            }
            if (b_Debug) { Debug.Print("TwoByte_ToBinMSBF result : " + result.ToString()); }
            return new string(result);
        }
        // Convert a two byte integer into a eight digit binary LSB first
        public string TwoByte_ToBinLSBF(int input)
        {
            char[] result = new char[8];
            for (int i = 0; i < 8; i++)
            {
                if ((input & 0x1) == 1) { result[i] = '1'; }
                else { result[i] = '0'; }
                input >>= 1;
            }
            if (b_Debug) { Debug.Print("TwoByte_ToBinMSBF result : " + result.ToString()); }
            return new string(result);
        }
    }
}
