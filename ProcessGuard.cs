using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ProjectToolRelease {
    public class Target {
        public uint Pid;
        public string Image;
        public string Cwd;
        public long Created;
    }
    public static class Guard {
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
        struct Entry {
            public uint Size, Usage, Pid;
            public UIntPtr Heap;
            public uint Module, Threads, Parent;
            public int Priority;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)] public string Name;
        }
        [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint flags, bool inherit, uint pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool ReadProcessMemory(IntPtr h, IntPtr address, byte[] data, UIntPtr size, out UIntPtr read);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool QueryFullProcessImageName(IntPtr h, uint flags, StringBuilder name, ref uint size);
        [DllImport("kernel32.dll")] static extern bool GetProcessTimes(IntPtr h, out long created, out long exited, out long kernel, out long user);
        [DllImport("kernel32.dll")] static extern bool IsWow64Process(IntPtr h, out bool wow);
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool TerminateProcess(IntPtr h, uint code);
        [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h, uint ms);
        [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern bool Process32FirstW(IntPtr h, ref Entry entry);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern bool Process32NextW(IntPtr h, ref Entry entry);
        [DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(IntPtr h, int cls, IntPtr[] info, int size, IntPtr returned);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        static readonly StringComparison CI = StringComparison.OrdinalIgnoreCase;

        static Dictionary<uint, Entry> Snapshot() {
            var map = new Dictionary<uint, Entry>();
            var h = CreateToolhelp32Snapshot(2, 0);
            if (h == new IntPtr(-1)) throw new System.ComponentModel.Win32Exception();
            try {
                Entry e = new Entry(); e.Size = (uint)Marshal.SizeOf(typeof(Entry));
                if (Process32FirstW(h, ref e)) do { map[e.Pid] = e; } while (Process32NextW(h, ref e));
            } finally { CloseHandle(h); }
            return map;
        }
        static string Image(IntPtr h) {
            var b = new StringBuilder(32768); uint count = (uint)b.Capacity;
            return QueryFullProcessImageName(h, 0, b, ref count) ? b.ToString() : null;
        }
        static long Birth(IntPtr h) {
            long a,b,d,e; return GetProcessTimes(h,out a,out b,out d,out e) ? a : 0;
        }
        static byte[] Read(IntPtr h, long addr, int length) {
            byte[] b = new byte[length]; UIntPtr read;
            return ReadProcessMemory(h,new IntPtr(addr),b,(UIntPtr)length,out read) && read.ToUInt64()==(ulong)length ? b : null;
        }
        static string Cwd(IntPtr h) {
            // Windows x64 PEB/RTL_USER_PROCESS_PARAMETERS layout. Fail closed for other architectures.
            bool wow;
            if(IntPtr.Size!=8 || !IsWow64Process(h,out wow) || wow) return null;
            var info = new IntPtr[6];
            if(NtQueryInformationProcess(h,0,info,48,IntPtr.Zero)!=0 || info[1]==IntPtr.Zero) return null;
            var p = Read(h,info[1].ToInt64()+0x20,8); if(p==null) return null;
            var s = Read(h,BitConverter.ToInt64(p,0)+0x38,16); if(s==null) return null;
            int length = BitConverter.ToUInt16(s,0);
            if(length==0 || length>32766 || length%2!=0) return null;
            var b = Read(h,BitConverter.ToInt64(s,8),length);
            return b==null ? null : Encoding.Unicode.GetString(b);
        }
        public static bool InMirror(string cwd, string root) {
            if(String.IsNullOrEmpty(cwd)) return false;
            try {
                string prefix = Path.GetFullPath(root).TrimEnd('\\')+"\\";
                string full = Path.GetFullPath(cwd);
                if(!full.StartsWith(prefix,CI)) return false;
                string project = full.Substring(prefix.Length).Split('\\')[0];
                return project.StartsWith("g-p-",CI) && project.Length>4;
            } catch { return false; }
        }
        public static bool ToolImage(string image) {
            if(String.IsNullOrEmpty(image)) return false;
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenAI","Codex","runtimes","cua_node")+"\\";
            if(!image.StartsWith(root,CI)) return false;
            string[] parts = image.Substring(root.Length).Split('\\');
            return parts.Length==3 && parts[0].Length>0 && parts[1].Equals("bin",CI)
                && (parts[2].Equals("node.exe",CI)||parts[2].Equals("node_repl.exe",CI));
        }
        static bool AppServerImage(string image) {
            if(String.IsNullOrEmpty(image) || !Path.GetFileName(image).Equals("codex.exe",CI)) return false;
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenAI","Codex","bin")+"\\";
            return image.StartsWith(root,CI);
        }
        static bool DesktopImage(string image) {
            if(String.IsNullOrEmpty(image)) return false;
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WindowsApps","OpenAI.Codex_");
            return image.StartsWith(root,CI) && image.EndsWith("\\app\\ChatGPT.exe",CI);
        }
        static bool DesktopAncestry(uint pid, Dictionary<uint,Entry> map, long childBirth) {
            Entry e;
            for(int depth=0;depth<8 && map.TryGetValue(pid,out e);depth++) {
                uint parent=e.Parent;
                if(parent==0 || parent==pid) return false;
                var h=OpenProcess(0x1000,false,parent); if(h==IntPtr.Zero) return false;
                string image; long created;
                try { image=Image(h); created=Birth(h); } finally { CloseHandle(h); }
                if(created==0 || created>childBirth) return false;
                if(AppServerImage(image)) {
                    Entry a; if(!map.TryGetValue(parent,out a)) return false;
                    var main=OpenProcess(0x1000,false,a.Parent); if(main==IntPtr.Zero) return false;
                    try { return DesktopImage(Image(main)) && Birth(main)>0 && Birth(main)<=created; }
                    finally { CloseHandle(main); }
                }
                if(!ToolImage(image)) return false;
                childBirth=created; pid=parent;
            }
            return false;
        }
        public static Target[] Scan(string root) {
            var map=Snapshot(); var result=new List<Target>();
            foreach(var pair in map) {
                var p=pair.Value;
                if(!p.Name.Equals("node.exe",CI) && !p.Name.Equals("node_repl.exe",CI)) continue;
                var h=OpenProcess(0x410,false,p.Pid); if(h==IntPtr.Zero) continue;
                try {
                    string image=Image(h); if(!ToolImage(image)) continue;
                    string cwd=Cwd(h); long created=Birth(h);
                    if(created>0 && InMirror(cwd,root) && DesktopAncestry(p.Pid,map,created))
                        result.Add(new Target {Pid=p.Pid,Image=image,Cwd=cwd,Created=created});
                } finally { CloseHandle(h); }
            }
            return result.ToArray();
        }
        public static string Stop(Target target, string root) {
            // Keep the handle through validation and termination so PID reuse cannot change the target.
            var h=OpenProcess(0x100411,false,target.Pid);
            if(h==IntPtr.Zero) return "unavailable-or-exited";
            try {
                if(WaitForSingleObject(h,0)==0) return "already-exited";
                if(Birth(h)!=target.Created || !String.Equals(Image(h),target.Image,CI)
                    || !ToolImage(target.Image) || !InMirror(Cwd(h),root)
                    || !DesktopAncestry(target.Pid,Snapshot(),target.Created)) return "identity-changed-skipped";
                if(!TerminateProcess(h,0)) return "failed-win32-"+Marshal.GetLastWin32Error();
                return WaitForSingleObject(h,2000)==0 ? "released" : "exit-pending";
            } finally { CloseHandle(h); }
        }
        public static int Probe(string directory) {
            // Tests access needed to replace a directory; never renames or deletes it.
            var h=CreateFileW(directory,0x10000,7,IntPtr.Zero,3,0x02000000,IntPtr.Zero);
            if(h==new IntPtr(-1)) return Marshal.GetLastWin32Error();
            CloseHandle(h); return 0;
        }
    }
}
