using System;
using System.Runtime.InteropServices;

namespace UnmanagedRefs
{
    internal abstract class IUnmanagedMemoryBlock
    {
        /// <summary>
        /// If this Unmanaged instance is actually wrapping GC-protected memory
        /// </summary>
        public bool IsWrappingManagedMemory { get; private set; }

        /// <summary>
        /// Number of bytes allocated to this unmanaged memory instance
        /// </summary>
        public int BytesAllocated { get; internal set; }

        /// <summary>
        /// Handle to the root of the allocated memory (4b sync + 4b rtti + values [ref] / values [val])
        /// </summary>
        public IntPtr Handle { get; internal set; }

        /// <summary>
        /// Handle to where the pointer needs to go in the allocated space for the CLR to recognize it as a managed type
        /// </summary>
        public abstract IntPtr BridgePointer { get; }

        public abstract void SetValueFromBytes(byte[] data);
        public virtual void SecureWipe() { }
        public abstract void CopyTo(IUnmanagedMemoryBlock target);
        public byte[] GetEntireSpace() => GetBytesFromHandle(Handle, BytesAllocated);
        public abstract byte[] GetValueSpace();

        protected IUnmanagedMemoryBlock(bool isWrappingManagedMemory = false) => IsWrappingManagedMemory = isWrappingManagedMemory;

        protected void Allocate(int bytes)
        {
            if (BytesAllocated > 0)
                SecureWipe();

            Release();

            Handle = Marshal.AllocHGlobal(bytes);
            BytesAllocated = bytes;
            ZeroFillPointer(Handle, BytesAllocated);
        }

        public virtual void Release()
        {
            if (!IsWrappingManagedMemory && Handle != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(Handle);
                Handle = IntPtr.Zero;
            }
        }

        protected byte[] GetBytesFromHandle(IntPtr handle, int numBytes)
        {
            var bytes = new byte[numBytes];
            Marshal.Copy(handle, bytes, 0, numBytes);
            return bytes;
        }

        protected void PointerToPointerCopy(IntPtr src, IntPtr dest, int numBytes)
        {
            var data = GetBytesFromHandle(src, numBytes);
            Marshal.Copy(data, 0, dest, numBytes);
        }

        protected void ZeroFillPointer(IntPtr target, int numBytes)
        {
            Marshal.Copy(new byte[numBytes], 0, target, numBytes);
        }
    }
}
