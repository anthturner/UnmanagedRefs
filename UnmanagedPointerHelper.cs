using System;
using System.Reflection.Emit;

namespace UnmanagedRefs
{
    internal static class UnmanagedPointerHelper
    {
        /// <summary>
        /// For any object allocated in the CLR, return the memory pointer of the object.
        /// 
        /// For reference types, this is the pointer to the RuntimeTypeHandle for the object (4b after the sync block, which is the root of the allocation)
        /// For value types, this is the pointer to the root of the allocation (value types do not have metadata)
        /// </summary>
        /// <typeparam name="T">Type to expect to retrieve a pointer for</typeparam>
        /// <param name="obj">Instance to retrieve a pointer for</param>
        /// <returns>Pointer (managed or unmanaged heap) to object</returns>
        internal static IntPtr GetNativeClrPointer<T>(this T obj) => UnmanagedPointerHelper<T>.GetPtrFromRef(obj);

        /// <summary>
        /// For any memory pointer to an object allocated in the CLR, return the runtime type instance.
        /// </summary>
        /// <typeparam name="T">Type to expect to receive an instance of</typeparam>
        /// <param name="ptr">Memory pointer to object address</param>
        /// <returns>Instance of object T</returns>
        internal static T GetNativeClrObject<T>(this IntPtr ptr) => UnmanagedPointerHelper<T>.GetRefFromPtr(ptr);
    }

    internal static class UnmanagedPointerHelper<T>
    {
#pragma warning disable S2743 // Static fields should not be used in generic types
        // Needed to correctly generate IL to convert between IntPtr<->T
        internal delegate T GetRefFromPtrDelegate(IntPtr ptr);
        internal static readonly GetRefFromPtrDelegate GetRefFromPtr;

        internal delegate IntPtr GetPtrFromRefDelegate(T obj);
        internal static readonly GetPtrFromRefDelegate GetPtrFromRef;
#pragma warning restore S2743 // Static fields should not be used in generic types

        static UnmanagedPointerHelper()
        {
            DynamicMethod m = new DynamicMethod("GetRefFromPtr", typeof(T), new[] { typeof(IntPtr) }, typeof(Unmanaged<T>), true);
            var il = m.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ret);
            GetRefFromPtr = (GetRefFromPtrDelegate)m.CreateDelegate(typeof(GetRefFromPtrDelegate));

            m = new DynamicMethod("GetPtrFromRef", typeof(IntPtr), new[] { typeof(T) }, typeof(Unmanaged<T>), true);
            il = m.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ret);
            GetPtrFromRef = (GetPtrFromRefDelegate)m.CreateDelegate(typeof(GetPtrFromRefDelegate));
        }
    }
}
