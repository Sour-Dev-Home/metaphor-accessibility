# Windows guard pages, VirtualQuery and ReadProcessMemory

- **Source:** Microsoft Learn, fetched on the date below:
  - "Creating Guard Pages", https://learn.microsoft.com/windows/win32/memory/creating-guard-pages (win32-pr commit
    `a955097414323a1a2517253d5454cccb022eaa6f`, updated 2025-03-12);
  - "Memory Protection Constants", https://learn.microsoft.com/windows/win32/memory/memory-protection-constants
    (win32-pr commit `cf79b24fde976a312f43c1d20abeb932adae51c9`, updated 2022-05-20);
  - "MEMORY_BASIC_INFORMATION", https://learn.microsoft.com/windows/win32/api/winnt/ns-winnt-memory_basic_information
    (updated 2024-02-22);
  - "VirtualQuery function", https://learn.microsoft.com/windows/win32/api/memoryapi/nf-memoryapi-virtualquery
    (updated 2025-07-01);
  - plus one observation of our own, with its steps, at the end.
- **Saved:** 2026-11-19
- **Game build:** none (this is about Windows, not the game)

Quoted excerpts, verbatim except that link targets are dropped; nothing else from these pages is relied on.

## Creating Guard Pages

> If a program attempts to access an address within a guard page, the system raises a **STATUS\_GUARD\_PAGE\_VIOLATION**
> (0x80000001) exception. The system also clears the **PAGE\_GUARD** modifier, removing the memory page's guard page
> status. The system will not stop the next attempt to access the memory page with a **STATUS\_GUARD\_PAGE\_VIOLATION**
> exception.
>
> If a guard page exception occurs during a system service, the service fails and typically returns some failure status
> indicator. Since the system also removes the relevant memory page's guard page status, the next invocation of the same
> system service won't fail due to a **STATUS\_GUARD\_PAGE\_VIOLATION** exception (unless, of course, someone
> reestablishes the guard page).

> A guard page provides a one-shot alarm for memory page access. This can be useful for an application that needs to
> monitor the growth of large dynamic data structures. For example, there are operating systems that use guard pages to
> implement automatic stack checking.

## Memory Protection Constants

| Constant | Value |
| --- | --- |
| PAGE_NOACCESS | 0x01 |
| PAGE_READONLY | 0x02 |
| PAGE_READWRITE | 0x04 |
| PAGE_WRITECOPY | 0x08 |
| PAGE_EXECUTE | 0x10 |
| PAGE_EXECUTE_READ | 0x20 |
| PAGE_EXECUTE_READWRITE | 0x40 |
| PAGE_EXECUTE_WRITECOPY | 0x80 |
| PAGE_GUARD | 0x100 |

> **PAGE\_GUARD** 0x100: Pages in the region become guard pages. Any attempt to access a guard page causes the system to
> raise a **STATUS\_GUARD\_PAGE\_VIOLATION** exception and turn off the guard page status. Guard pages thus act as a
> one-time access alarm. [...] When an access attempt leads the system to turn off guard page status, the underlying page
> protection takes over. If a guard page exception occurs during a system service, the service typically returns a
> failure status indicator. This value cannot be used with **PAGE\_NOACCESS**.

## MEMORY_BASIC_INFORMATION

> `RegionSize`: The size of the region beginning at the base address in which all pages have identical attributes, in
> bytes.

| State | Meaning |
| --- | --- |
| MEM_COMMIT 0x1000 | Indicates committed pages for which physical storage has been allocated, either in memory or in the paging file on disk. |
| MEM_FREE 0x10000 | Indicates free pages not accessible to the calling process and available to be allocated. For free pages, the information in the **AllocationBase**, **AllocationProtect**, **Protect**, and **Type** members is undefined. |
| MEM_RESERVE 0x2000 | Indicates reserved pages where a range of the process's virtual address space is reserved without any physical storage being allocated. For reserved pages, the information in the **Protect** member is undefined. |

## VirtualQuery

> If *lpAddress* specifies an address above the highest memory address accessible to the process, the function fails
> with **ERROR\_INVALID\_PARAMETER**.

> If the function fails, the return value is zero.

> **VirtualQuery** provides information about a region of consecutive pages beginning at a specified address that share
> the following attributes: [...] The access granted to all pages is the same (**PAGE\_READONLY**, **PAGE\_READWRITE**,
> **PAGE\_NOACCESS**, **PAGE\_WRITECOPY**, **PAGE\_EXECUTE**, **PAGE\_EXECUTE\_READ**, **PAGE\_EXECUTE\_READWRITE**,
> **PAGE\_EXECUTE\_WRITECOPY**, **PAGE\_GUARD**, or **PAGE\_NOCACHE**).

## Observation: ReadProcessMemory on a guard page in the calling process

- **Setup:** Windows 11 Pro 10.0.26200, .NET 9 x64, xUnit, from a probe version of
  `tests/Metaphor.Core.Tests/PageRegionWindowsTests.cs` (#393).
- **Steps:** `VirtualAlloc` three pages `MEM_RESERVE`; commit the second `PAGE_READWRITE | PAGE_GUARD`; call
  `ReadProcessMemory(GetCurrentProcess() pseudo-handle -1, page 2, 8 bytes)`; `VirtualQuery` page 2; call
  `ReadProcessMemory` again.
- **Seen:** both calls returned FALSE with 0 bytes read, and `VirtualQuery` still reported `PAGE_GUARD` after the first.
  So on this build `ReadProcessMemory` failed without clearing the guard, unlike the general system-service rule quoted
  above. One build only; whether it holds elsewhere is `[NEEDS VERIFICATION]`.
