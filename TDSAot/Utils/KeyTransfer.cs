using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Collections.Generic;

namespace TDSAot.Utils
{
    public class KeyTransfer
    {
        public static uint TransKey(string keyStr)
        {
            return keyStr switch
            {
                "F1"    => 112,
                "F2"    => 113,
                "F3"    => 114,
                "F4"    => 115,
                "F5"    => 116,
                "F6"    => 117,
                "F7"    => 118,
                "F8"    => 119,
                "F9"    => 120,
                "F10"   => 121,
                "F11"   => 122,
                "F12"   => 123,
                "~"     => 192,
                "Esc"   => 27,
                "Space" => 32,
                "."     => 190,
                "1"     => 49,
                "2"     => 50,
                "3"     => 51,
                "4"     => 52,
                "5"     => 53,
                "6"     => 54,
                "7"     => 55,
                "8"     => 56,
                "9"     => 57,
                "0"     => 48,
                "z"     => 90,
                "x"     => 88,
                "c"     => 67,
                "="     => 187,
                "-"     => 189,
                "\\"    => 220,
                "/"     => 191,
                "Ctrl"  => (uint)KeyModifiers.Control,
                "Alt"   => (uint)KeyModifiers.Alt,
                "Shift" => (uint)KeyModifiers.Shift,
                "Win"   => (uint)KeyModifiers.Meta,
                _       => 192
            };
        }

        public static string ReverseTransKey(uint keyValue)
        {
            return keyValue switch
            {
                112 => "F1",
                113 => "F2",
                114 => "F3",
                115 => "F4",
                116 => "F5",
                117 => "F6",
                118 => "F7",
                119 => "F8",
                120 => "F9",
                121 => "F10",
                122 => "F11",
                123 => "F12",
                192 => "~",
                27 => "Esc",
                32 => "Space",
                190 => ".",
                49 => "1",
                50 => "2",
                51 => "3",
                52 => "4",
                53 => "5",
                54 => "6",
                55 => "7",
                56 => "8",
                57 => "9",
                48 => "0",
                90 => "z",
                88 => "x",
                67 => "c",
                187 => "=",
                189 => "-",
                220 => "\\",
                191 => "/",
                uint n when n == (uint)KeyModifiers.Control => "Ctrl",
                uint n when n == (uint)KeyModifiers.Alt => "Alt",
                uint n when n == (uint)KeyModifiers.Shift => "Shift",
                uint n when n == (uint)KeyModifiers.Meta => "Win",
                _ => "~" // 默认值与原函数保持一致
            };
        }
    }
}