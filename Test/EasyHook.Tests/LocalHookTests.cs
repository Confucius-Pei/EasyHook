﻿// EasyLoad (File: EasyLoad\Loader.cs)
//
// Copyright (c) 2015 Justin Stenning
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
// Please visit https://easyhook.github.io for more information
// about the project and latest updates.

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EasyHook.Tests
{
    [TestClass]
    public class LocalHookTests
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool Beep(uint dwFreq, uint dwDuration);

        [return: MarshalAs(UnmanagedType.Bool)]
        delegate bool BeepDelegate(uint dwFreq, uint dwDuration);

        bool _beepHookCalled;

        [return: MarshalAs(UnmanagedType.Bool)]
        bool BeepHook(uint dwFreq, uint dwDuration)
        {
            _beepHookCalled = true;
            Beep(dwFreq, dwDuration);
            return false;
        }

        [TestInitialize]
        public void Initialize()
        {
            NativeAPI.LhWaitForPendingRemovals();
        }

        [TestMethod]
        [ExpectedException(typeof(InsufficientMemoryException),
            "Adding too many hooks should result in System.InsufficientMemoryException.")]
        public void InstallTooManyHooks_ThrowException()
        {
            int maxHookCount = 1024;

            List<LocalHook> hooks = new List<LocalHook>();

            // Install MAX_HOOK_COUNT hooks (i.e. 1024)
            for (var i = 0; i < maxHookCount; i++)
            {
                LocalHook lh = LocalHook.Create(
                    LocalHook.GetProcAddress("kernel32.dll", "Beep"),
                    new BeepDelegate(BeepHook),
                    this);
                hooks.Add(lh);
            }

            // NOTE: Disposing hooks does not free the memory and we need to also
            // call NativeAPI.LhWaitForPendingRemovals() or LocalHook.Release();
            foreach (var h in hooks)
                h.Dispose();
            hooks.Clear();

            try
            {
                // Adding one more hook should result in System.InsufficientMemoryException
                hooks.Add(LocalHook.Create(
                    LocalHook.GetProcAddress("kernel32.dll", "Beep"),
                    new BeepDelegate(BeepHook),
                    this));

                foreach (var h in hooks)
                    h.Dispose();
                hooks.Clear();
            }
            finally
            {
                // Ensure the hooks are freed
                NativeAPI.LhWaitForPendingRemovals();

                foreach (var h in hooks)
                    h.Dispose();
                hooks.Clear();
            }
        }

        [TestMethod]
        public void HookBypassAddress_DoesNotCallHook()
        {
            // Install MAX_HOOK_COUNT hooks (i.e. 1024)
            LocalHook localHook = LocalHook.Create(
                LocalHook.GetProcAddress("kernel32.dll", "Beep"),
                new BeepDelegate(BeepHook),
                this);

            localHook.ThreadACL.SetInclusiveACL(new int[] { 0 });

            Assert.IsFalse(Beep(100, 100));

            Assert.IsTrue(_beepHookCalled);

            _beepHookCalled = false;

            BeepDelegate beepDelegate = (BeepDelegate)Marshal.GetDelegateForFunctionPointer(
                localHook.HookBypassAddress, typeof(BeepDelegate));

            beepDelegate(100, 100);
            Assert.IsFalse(_beepHookCalled);
        }
    }
}
