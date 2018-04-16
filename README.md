# UnmanagedRefs
_Encapsulate and handle managed references in the CLR's unmanaged memory space_

## Usage:
### Explicit Creation
```csharp
var unmanagedInt = new Unmanaged<int>(42);
```

### Implicit Creation
```csharp
int myInt = 42;
Unmanaged<int> myUnmanagedInt = (Unmanaged<int>)myInt;
```

### Creation/Assignment
```csharp
Unmanaged<int> myInt = new Unmanaged<int>();
myInt = 42; // normal assignment maps to unmanaged memory
```

### Object Reference Tracking
```csharp
var unmanagedIntArr = new Unmanaged<int[]>(new int[] { 1, 2, 3, 4, 5 });
var intArr = (int[])unmanagedIntArr;
var recastIntArr = (Unmanaged<int[]>)intArr;

intArr[0] = 42;

//recastIntArr is now { 42, 2, 3, 4, 5 }
```

### Memory Map Exports
```csharp
var unmanagedStr = new Unmanaged<string>("This string is interned, so this operation will leak this source data.");
var bytes = unmanagedStr.Export();
/* ... store the bytes with some Type representation, so on the next run ... */
var newStr = Unmanaged<string>.CreateFromExport(bytes); // this instance of the application won't have leaked the interned string!
```