// Answers one question about the local Ghidra project of the game exe (#290). Run by scripts/ghidra.ps1 on a project
// opened read-only, so nothing a query does (such as disassembling a function in a section auto-analysis skipped) is
// saved. Arguments: <output file> <command> <target> [<extra>]. A target is an address, as an RVA (0x21e1c0, the form
// the raw sources use) or a full address (0x14021e1c0), or a symbol name (CursorManagerInput, Package::vftable).
//
// Commands:
//   decompile <target>          the function's C, from Ghidra's decompiler
//   disasm <target>             the function's instructions
//   callers <target>            functions that call it, with each call site
//   callees <target>            functions it calls
//   offset <offset> [<target>]  instructions with a memory operand at that displacement ([reg + offset], not RSP or
//                               RIP), in one function or in all analysed code, marked read, write, addr (lea) or call
//   global <target>             instructions whose RIP-relative operand is that global ([rip + disp32] resolving to
//                               it), in all analysed code, marked read, write, rw, addr (lea) or call (a call or
//                               jump through it). It scans the instructions rather than trusting the reference table,
//                               so it doesn't depend on which references auto-analysis made.
//   rtti <target>               the RTTI class of a vtable, of an object holding a vtable pointer, or of each vtable
//                               holding a pointer to a function
//   vtable <target>             a vtable's class and slots; the target is the vtable or a class name
//
// RTTI layout (x64 MSVC): the qword before a vtable points to a CompleteObjectLocator whose dword at +0 is the
// signature (1), at +12 the TypeDescriptor's RVA and at +20 its own RVA; the TypeDescriptor's name starts at +0x10.
// Source: Ghidra 12.2's Rtti4Model.java and TypeDescriptorModel.java (MicrosoftCodeAnalyzer-src.zip in the install).
//@category Metaphor

import java.io.PrintWriter;
import java.util.ArrayList;
import java.util.List;

import ghidra.app.cmd.disassemble.DisassembleCommand;
import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.address.AddressSet;
import ghidra.program.model.lang.OperandType;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.mem.MemoryBlock;
import ghidra.program.model.scalar.Scalar;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.SourceType;
import ghidra.program.model.symbol.Symbol;

public class MetaphorQuery extends GhidraScript {

	private static final int MAX_LINES = 500;
	private static final int MAX_SLOTS = 1024;
	private static final int ON_DEMAND_BYTES = 0x10000;
	private static final int NAME_REACH = 0x400;

	private PrintWriter out;
	private long imageBase;

	@Override
	protected void run() throws Exception {
		String[] args = getScriptArgs();
		if (args.length < 3) {
			throw new IllegalArgumentException("usage: MetaphorQuery.java <output file> <command> <target> [<extra>]");
		}
		imageBase = currentProgram.getImageBase().getOffset();
		try (PrintWriter writer = new PrintWriter(args[0], "UTF-8")) {
			out = writer;
			String extra = args.length > 3 ? args[3] : null;
			try {
				switch (args[1]) {
					case "decompile" -> decompile(function(args[2]));
					case "disasm" -> disasm(function(args[2]));
					case "callers" -> callers(function(args[2]));
					case "callees" -> callees(function(args[2]));
					case "offset" -> offset(parseNumber(args[2]), extra == null ? null : function(extra));
					case "global" -> global(address(args[2]));
					case "rtti" -> rtti(address(args[2]));
					case "vtable" -> {
						for (Address start : vtableAddresses(args[2])) {
							vtable(start);
						}
					}
					default -> throw new IllegalArgumentException("unknown command " + args[1]);
				}
			}
			catch (IllegalArgumentException e) {
				// A bad target: scripts/ghidra.ps1 prints this line and exits 1.
				out.println("# error: " + e.getMessage());
			}
		}
	}

	// --- Targets ---

	private static long parseNumber(String text) {
		String t = text.toLowerCase();
		return t.startsWith("0x") ? Long.parseUnsignedLong(t.substring(2), 16) : Long.parseLong(t);
	}

	private Address address(String target) {
		if (target.matches("(?i)0x[0-9a-f]+|[0-9]+")) {
			long value = parseNumber(target);
			return toAddr(value < imageBase ? imageBase + value : value);
		}
		List<Symbol> found = new ArrayList<>();
		String last = target.contains("::") ? target.substring(target.lastIndexOf("::") + 2) : target;
		for (Symbol symbol : currentProgram.getSymbolTable().getSymbols(last)) {
			if (symbol.getName(true).equals(target) || symbol.getName().equals(target)) {
				found.add(symbol);
			}
		}
		if (found.size() != 1) {
			throw new IllegalArgumentException(found.size() + " symbols named " + target + (found.isEmpty() ? ""
				: ": " + found.stream().map(s -> s.getName(true) + " " + rva(s.getAddress())).toList()));
		}
		return found.get(0).getAddress();
	}

	// The function at or containing the target, disassembled and created on demand (read-only, so not saved) when
	// auto-analysis didn't reach it, e.g. in .text1.
	private Function function(String target) throws Exception {
		Address address = address(target);
		Function function = getFunctionContaining(address);
		if (function != null) {
			return function;
		}
		if (getInstructionAt(address) == null) {
			var block = currentProgram.getMemory().getBlock(address);
			if (block == null || !block.isExecute()) {
				throw new IllegalArgumentException(rva(address) + " is not in executable memory");
			}
			Address end = address.add(Math.min(ON_DEMAND_BYTES, block.getEnd().subtract(address)));
			new DisassembleCommand(address, new AddressSet(address, end), true).applyTo(currentProgram, monitor);
		}
		function = createFunction(address, null);
		if (function == null) {
			throw new IllegalArgumentException("no function at " + rva(address));
		}
		out.println("# " + rva(address) + ": function created for this query only (not in the saved project)");
		return function;
	}

	private String rva(Address address) {
		return String.format("0x%x", address.getOffset() - imageBase);
	}

	private String describe(Function function) {
		return function.getName(true) + " @ " + rva(function.getEntryPoint());
	}

	private void header(String what, Function function) {
		out.println("# " + what + " " + describe(function) + " (addresses are RVAs; build: see project folder)");
	}

	// --- Commands ---

	private void decompile(Function function) {
		header("decompile", function);
		DecompInterface decompiler = new DecompInterface();
		try {
			decompiler.openProgram(currentProgram);
			DecompileResults results = decompiler.decompileFunction(function, 120, monitor);
			if (!results.decompileCompleted()) {
				out.println("# decompiler failed: " + results.getErrorMessage());
				return;
			}
			out.print(results.getDecompiledFunction().getC());
		}
		finally {
			decompiler.dispose();
		}
	}

	private void disasm(Function function) {
		header("disasm", function);
		int lines = 0;
		for (Instruction instruction : currentProgram.getListing().getInstructions(function.getBody(), true)) {
			if (++lines > MAX_LINES * 4) {
				out.println("# stopped after " + (MAX_LINES * 4) + " instructions");
				break;
			}
			Symbol label = getSymbolAt(instruction.getAddress());
			if (label != null && !instruction.getAddress().equals(function.getEntryPoint())) {
				out.println(label.getName() + ":");
			}
			out.printf("  %s  %s%n", rva(instruction.getAddress()), instruction);
		}
	}

	private void callers(Function function) {
		header("callers of", function);
		int lines = 0;
		for (Reference reference : getReferencesTo(function.getEntryPoint())) {
			if (++lines > MAX_LINES) {
				out.println("# stopped after " + MAX_LINES);
				break;
			}
			Function caller = getFunctionContaining(reference.getFromAddress());
			out.printf("%s  %-14s %s%n", rva(reference.getFromAddress()), reference.getReferenceType(),
				caller == null ? "(no function: data or unanalysed code)" : describe(caller));
		}
	}

	private void callees(Function function) {
		header("callees of", function);
		for (Instruction instruction : currentProgram.getListing().getInstructions(function.getBody(), true)) {
			if (!instruction.getFlowType().isCall()) {
				continue;
			}
			Address[] flows = instruction.getFlows();
			String target = "(indirect) " + instruction;
			if (flows.length > 0) {
				Function callee = getFunctionAt(flows[0]);
				target = callee == null ? rva(flows[0]) : describe(callee);
			}
			out.printf("%s  %s%n", rva(instruction.getAddress()), target);
		}
	}

	private void offset(long displacement, Function only) {
		out.printf("# memory operands at displacement 0x%x in %s%n", displacement,
			only == null ? "all analysed code" : describe(only));
		var instructions = only == null ? currentProgram.getListing().getInstructions(true)
				: currentProgram.getListing().getInstructions(only.getBody(), true);
		int lines = 0;
		for (Instruction instruction : instructions) {
			if (monitor.isCancelled()) {
				break;
			}
			for (int i = 0; i < instruction.getNumOperands(); i++) {
				if (!hasDisplacement(instruction, i, displacement)) {
					continue;
				}
				if (++lines > MAX_LINES) {
					out.println("# stopped after " + MAX_LINES + " matches; name a function to narrow it");
					return;
				}
				Function in = getFunctionContaining(instruction.getAddress());
				String mnemonic = instruction.getMnemonicString().toUpperCase();
				String access = mnemonic.equals("LEA") ? "addr"
						: instruction.getFlowType().isCall() ? "call"
						: i == 0 && instruction.getNumOperands() > 1 ? "write" : "read";
				out.printf("%s  %-5s %-40s %s%n", rva(instruction.getAddress()), access, instruction,
					in == null ? "" : describe(in));
				break;
			}
		}
	}

	// True when operand i addresses memory as [register + displacement] (an lea counts too). RIP-relative operands are
	// globals and RSP-based ones stack slots, so neither counts.
	private static boolean hasDisplacement(Instruction instruction, int i, long displacement) {
		int type = instruction.getOperandType(i);
		if ((type & OperandType.DYNAMIC) == 0) {
			return false;
		}
		boolean register = false;
		boolean match = false;
		for (Object part : instruction.getOpObjects(i)) {
			if (part instanceof ghidra.program.model.lang.Register r) {
				if (r.getName().equalsIgnoreCase("RSP") || r.getName().equalsIgnoreCase("RIP")) {
					return false;
				}
				register = true;
			}
			else if (part instanceof Scalar s) {
				match |= s.getSignedValue() == displacement;
			}
		}
		return register && match;
	}

	private void global(Address target) {
		out.printf("# instructions addressing the global %s in all analysed code%n", rva(target));
		int lines = 0;
		for (Instruction instruction : currentProgram.getListing().getInstructions(true)) {
			if (monitor.isCancelled()) {
				break;
			}
			for (int i = 0; i < instruction.getNumOperands(); i++) {
				if (!addresses(instruction, i, target)) {
					continue;
				}
				if (++lines > MAX_LINES) {
					out.println("# stopped after " + MAX_LINES + " matches");
					return;
				}
				out.printf("%s  %-5s %-40s %s%n", rva(instruction.getAddress()), access(instruction, i, target),
					instruction, container(instruction.getAddress()));
				break;
			}
		}
		if (lines == 0) {
			out.println("# none found");
		}
	}

	// The function holding an instruction. Code reached only by a jump (such as a script function's body) has none, so
	// then the nearest name before it that the build or a person gave, such as a hook-signatures.json name, if close.
	private String container(Address at) {
		Function function = getFunctionContaining(at);
		if (function != null) {
			return describe(function);
		}
		var symbols = currentProgram.getSymbolTable().getSymbolIterator(at, false);
		while (symbols.hasNext()) {
			Symbol symbol = symbols.next();
			if (at.subtract(symbol.getAddress()) > NAME_REACH) {
				break;
			}
			if (symbol.getSource() != SourceType.DEFAULT) {
				return "(no function; " + symbol.getName(true) + " @ " + rva(symbol.getAddress()) + ")";
			}
		}
		return "(no function)";
	}

	// True when operand i is a memory operand or address at the target: Ghidra resolves [rip + disp32] to the absolute
	// address, as an Address operand object or as the operand's reference.
	private static boolean addresses(Instruction instruction, int i, Address target) {
		for (Reference reference : instruction.getOperandReferences(i)) {
			if (reference.getToAddress().equals(target)) {
				return true;
			}
		}
		boolean memory = (instruction.getOperandType(i) & (OperandType.ADDRESS | OperandType.DYNAMIC)) != 0;
		for (Object part : instruction.getOpObjects(i)) {
			if (part instanceof Address a && a.equals(target)
				|| memory && part instanceof Scalar s && s.getUnsignedValue() == target.getOffset()) {
				return true;
			}
		}
		return false;
	}

	// lea takes the address; a call through it reads it as a pointer. Otherwise the operand's reference type says, and
	// when there is none, the destination operand of a two-operand instruction other than cmp and test is written.
	private static String access(Instruction instruction, int i, Address target) {
		String mnemonic = instruction.getMnemonicString().toUpperCase();
		if (mnemonic.equals("LEA")) {
			return "addr";
		}
		if (instruction.getFlowType().isCall() || instruction.getFlowType().isJump()) {
			return "call";
		}
		for (Reference reference : instruction.getOperandReferences(i)) {
			if (reference.getToAddress().equals(target)) {
				var type = reference.getReferenceType();
				if (type.isRead() && type.isWrite()) {
					return "rw";
				}
				if (type.isWrite()) {
					return "write";
				}
				if (type.isRead()) {
					return "read";
				}
			}
		}
		boolean compares = mnemonic.startsWith("CMP") || mnemonic.equals("TEST");
		return i == 0 && instruction.getNumOperands() > 1 && !compares ? "write" : "read";
	}

	private void rtti(Address address) throws Exception {
		String own = className(address);
		if (own != null) {
			out.println(rva(address) + "  vtable of " + own);
			return;
		}
		MemoryBlock block = currentProgram.getMemory().getBlock(address);
		if (block != null && !block.isExecute()) {
			Address pointed = pointerAt(address);
			String held = pointed == null ? null : className(pointed);
			if (held != null) {
				out.println(rva(address) + "  object whose vtable " + rva(pointed) + " is " + held);
				return;
			}
		}
		Function function = getFunctionContaining(address);
		Address entry = function == null ? address : function.getEntryPoint();
		int found = 0;
		for (Reference reference : getReferencesTo(entry)) {
			Address slot = reference.getFromAddress();
			if (currentProgram.getMemory().getBlock(slot).isExecute()) {
				continue;
			}
			for (int i = 0; i < MAX_SLOTS; i++) {
				Address start = slot.subtract(8L * i);
				String name = className(start);
				if (name != null) {
					out.printf("%s  slot %d of vtable %s, class %s%n", rva(entry), i, rva(start), name);
					found++;
					break;
				}
			}
		}
		if (found == 0) {
			out.println(rva(address) + "  no RTTI class found (not a vtable, an object or a vtable entry)");
		}
	}

	private void vtable(Address start) throws Exception {
		String name = className(start);
		out.println("# vtable " + rva(start) + (name == null ? " (no RTTI found before it)" : ", class " + name));
		for (int i = 0; i < MAX_SLOTS; i++) {
			Address slot = start.add(8L * i);
			if (i > 0 && (className(slot) != null || getSymbolAt(slot) != null && getSymbolAt(slot).getName()
					.contains("vftable"))) {
				break;
			}
			Address target = pointerAt(slot);
			MemoryBlock block = target == null ? null : currentProgram.getMemory().getBlock(target);
			if (block == null || !block.isExecute()) {
				break;
			}
			Function function = getFunctionAt(target);
			out.printf("%3d  +0x%03x  %s%n", i, 8 * i, function == null ? rva(target) : describe(function));
		}
	}

	// The vtable at an address, or every vftable of a class (one per base with virtual functions).
	private List<Address> vtableAddresses(String target) {
		if (target.matches("(?i)0x[0-9a-f]+|[0-9]+")) {
			return List.of(address(target));
		}
		String name = target.endsWith("vftable") ? target : target + "::vftable";
		List<Address> found = new ArrayList<>();
		for (Symbol symbol : currentProgram.getSymbolTable().getSymbols("vftable")) {
			if (symbol.getName(true).equals(name) && !found.contains(symbol.getAddress())) {
				found.add(symbol.getAddress());
			}
		}
		if (found.isEmpty()) {
			throw new IllegalArgumentException("no vftable named " + name);
		}
		return found;
	}

	// --- RTTI ---

	private Address pointerAt(Address address) {
		try {
			long value = currentProgram.getMemory().getLong(address);
			Address pointed = toAddr(value);
			return currentProgram.getMemory().contains(pointed) ? pointed : null;
		}
		catch (Exception e) {
			return null;
		}
	}

	// The class name when the qword before this address points to a valid CompleteObjectLocator, else null.
	private String className(Address vtable) {
		try {
			var memory = currentProgram.getMemory();
			Address locator = pointerAt(vtable.subtract(8));
			if (locator == null || memory.getInt(locator) != 1 ||
				Integer.toUnsignedLong(memory.getInt(locator.add(20))) != locator.getOffset() - imageBase) {
				return null;
			}
			Address descriptor = toAddr(imageBase + Integer.toUnsignedLong(memory.getInt(locator.add(12))));
			StringBuilder name = new StringBuilder();
			for (Address a = descriptor.add(16); name.length() < 512; a = a.add(1)) {
				byte b = memory.getByte(a);
				if (b == 0) {
					break;
				}
				name.append((char) b);
			}
			return demangle(name.toString());
		}
		catch (Exception e) {
			return null;
		}
	}

	// ".?AVScreen@ui@@" -> "ui::Screen (.?AVScreen@ui@@)"; templates and other forms stay as written.
	private static String demangle(String mangled) {
		if (!mangled.matches("\\.\\?A[VU][A-Za-z0-9_@]+@@")) {
			return mangled;
		}
		String[] parts = mangled.substring(4, mangled.length() - 2).split("@");
		StringBuilder plain = new StringBuilder();
		for (int i = parts.length - 1; i >= 0; i--) {
			plain.append(parts[i]).append(i > 0 ? "::" : "");
		}
		return plain + " (" + mangled + ")";
	}
}
