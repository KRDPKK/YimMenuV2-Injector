using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace YY
{
    public partial class yy : Form
    {
        public yy() => InitializeComponent();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint size, uint allocType, uint protect);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] buffer, uint size, out IntPtr written);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes,
            uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [DllImport("ntdll.dll", SetLastError = true)]
        static extern uint NtCreateThreadEx(
            out IntPtr threadHandle,
            uint desiredAccess,
            IntPtr objectAttributes,
            IntPtr processHandle,
            IntPtr startAddress,
            IntPtr parameter,
            bool createSuspended,
            uint stackZeroBits,
            uint sizeOfStackCommit,
            uint sizeOfStackReserve,
            IntPtr bytesBuffer);

        const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
        const uint MEM_COMMIT = 0x1000;
        const uint PAGE_READWRITE = 0x04;
        const uint THREAD_ALL_ACCESS = 0x1FFFFF;

        enum InjectionMethod
        {
            CreateRemoteThread = 1,
            NtCreateThreadEx = 2
        }

        private void btnInject_Click(object sender, EventArgs e)
        {
            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "YimMenuV2.dll");
            if (!File.Exists(dllPath))
            {
                MessageBox.Show("YimMenuV2.dll not found!");
                return;
            }

            Process[] targets = Process.GetProcessesByName("GTA5_Enhanced");
            if (targets.Length == 0)
            {
                MessageBox.Show("GTA5_Enhanced.exe not running.");
                return;
            }

            // Prompt user to choose injection method
            var choice = MessageBox.Show("Choose injection method:\nYes = CreateRemoteThread\nNo = NtCreateThreadEx",
                                         "Select Injection Method", MessageBoxButtons.YesNoCancel);

            if (choice == DialogResult.Cancel)
                return;

            InjectionMethod injectionMethod = (choice == DialogResult.Yes) ? InjectionMethod.CreateRemoteThread : InjectionMethod.NtCreateThreadEx;

            int successCount = 0;
            int failCount = 0;

            foreach (Process gta in targets)
            {
                bool injected = false;

                IntPtr hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, gta.Id);
                if (hProcess == IntPtr.Zero)
                {
                    failCount++;
                    continue;
                }

                IntPtr allocMem = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)(dllPath.Length + 1), MEM_COMMIT, PAGE_READWRITE);
                if (allocMem == IntPtr.Zero)
                {
                    failCount++;
                    continue;
                }

                byte[] bytes = Encoding.ASCII.GetBytes(dllPath);
                if (!WriteProcessMemory(hProcess, allocMem, bytes, (uint)bytes.Length, out _))
                {
                    failCount++;
                    continue;
                }

                IntPtr loadLibAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
                if (loadLibAddr == IntPtr.Zero)
                {
                    failCount++;
                    continue;
                }

                if (injectionMethod == InjectionMethod.CreateRemoteThread)
                {
                    IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, loadLibAddr, allocMem, 0, IntPtr.Zero);
                    if (hThread != IntPtr.Zero)
                        injected = true;
                }
                else if (injectionMethod == InjectionMethod.NtCreateThreadEx)
                {
                    injected = NtCreateThreadExInject(hProcess, loadLibAddr, allocMem);
                }

                if (injected)
                    successCount++;
                else
                    failCount++;
            }

            MessageBox.Show($"Injection finished.\nSuccess: {successCount}\nFailed: {failCount}");
        }

        private bool NtCreateThreadExInject(IntPtr hProcess, IntPtr loadLibraryAddr, IntPtr allocMem)
        {
            IntPtr threadHandle;
            uint status = NtCreateThreadEx(
                out threadHandle,
                THREAD_ALL_ACCESS,
                IntPtr.Zero,
                hProcess,
                loadLibraryAddr,
                allocMem,
                false,
                0,
                0,
                0,
                IntPtr.Zero);

            return status == 0 && threadHandle != IntPtr.Zero;
        }

        private void label2_Click(object sender, EventArgs e)
        {
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/YimMenu/YimMenuV2/releases/tag/nightly");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/KRDPKK/YimMenuV2-Injector");
        }
    }
}
