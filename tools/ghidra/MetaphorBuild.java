// Builds the local Ghidra project of the game exe (#290): auto-analysis of the game's own code, then the names we know.
// Run by scripts/ghidra.ps1, one step per headless run so each saves its work: "analyse" right after an import with
// -noanalysis, then "names <hook-signatures.json> <game build>", then "vtables".
//
// Only reads the exe as imported from disk (AGENTS.md rule 5, docs/raw-sources/drm-status-2026-11-13.md): nothing is
// decrypted, unpacked or run. The sections listed in SKIPPED (.text1, .xcode, .xtext: the protection's, ADR 0010) are
// left out of auto-analysis and made non-executable while it runs, so disassembly never flows into them. Their
// executable flag is restored afterwards, and a query disassembles one function there on demand.
//@category Metaphor

import java.io.FileReader;
import java.util.ArrayList;
import java.util.List;
import java.util.Set;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import ghidra.app.plugin.core.analysis.AutoAnalysisManager;
import ghidra.app.script.GhidraScript;
import ghidra.program.disassemble.Disassembler;
import ghidra.program.model.address.Address;
import ghidra.program.model.address.AddressSet;
import ghidra.program.model.listing.Program;
import ghidra.program.model.mem.MemoryBlock;
import ghidra.program.model.symbol.SourceType;
import ghidra.program.util.GhidraProgramUtilities;

public class MetaphorBuild extends GhidraScript {

	private static final Set<String> SKIPPED = Set.of(".text1", ".xcode", ".xtext");

	@Override
	protected void run() throws Exception {
		String[] args = getScriptArgs();
		if (args.length == 1 && args[0].equals("analyse")) {
			analyse();
			GhidraProgramUtilities.markProgramAnalyzed(currentProgram);
		}
		else if (args.length == 3 && args[0].equals("names")) {
			importSignatures(args[1], args[2]);
		}
		else if (args.length == 1 && args[0].equals("vtables")) {
			nameVirtualFunctions();
		}
		else {
			throw new IllegalArgumentException(
				"usage: MetaphorBuild.java analyse | names <hook-signatures.json> <game build> | vtables");
		}
	}

	// Names each unnamed function in a vtable the RTTI analyzer labelled <Class>::vftable as <Class>::vf<slot>, so
	// decompiled code reads as calls on classes. Replaces Ghidra's RecoverClassesFromRTTIScript.java, which ran for
	// over 50 minutes on one core decompiling .text1 bodies that don't decode (2026-11-17, #290). A function in
	// several vtables (an inherited one) takes the first class by vtable address, and its plate comment lists the
	// others. Slots in the skipped sections stay unnamed: they hold no analysed function.
	private void nameVirtualFunctions() throws Exception {
		var memory = currentProgram.getMemory();
		var seen = new java.util.HashMap<Address, List<String>>();
		var owner = new java.util.HashMap<Address, ghidra.program.model.symbol.Namespace>();
		var slotOf = new java.util.HashMap<Address, Integer>();
		List<Address> tables = new ArrayList<>();
		for (var symbol : currentProgram.getSymbolTable().getSymbols("vftable")) {
			tables.add(symbol.getAddress());
		}
		tables.sort(null);
		int vtables = 0;
		for (Address table : tables) {
			monitor.checkCancelled();
			var symbol = currentProgram.getSymbolTable().getPrimarySymbol(table);
			var symbols = currentProgram.getSymbolTable().getSymbols(table);
			ghidra.program.model.symbol.Namespace cls = null;
			for (var s : symbols) {
				if (s.getName().equals("vftable")) {
					cls = s.getParentNamespace();
				}
			}
			if (cls == null || symbol == null) {
				continue;
			}
			vtables++;
			for (int slot = 0; slot < 1024; slot++) {
				Address entry = table.add(8L * slot);
				if (slot > 0 && currentProgram.getSymbolTable().getPrimarySymbol(entry) != null) {
					break; // the next vtable, or its locator pointer
				}
				Address target;
				try {
					target = toAddr(memory.getLong(entry));
				}
				catch (Exception e) {
					break;
				}
				MemoryBlock block = memory.getBlock(target);
				if (block == null || !block.isExecute()) {
					break;
				}
				if (SKIPPED.contains(block.getName())) {
					continue;
				}
				var function = getFunctionAt(target);
				if (function == null) {
					continue;
				}
				String where = cls.getName(true) + "::vf" + slot;
				seen.computeIfAbsent(target, k -> new ArrayList<>()).add(where);
				if (!owner.containsKey(target)) {
					owner.put(target, cls);
					slotOf.put(target, slot);
				}
			}
		}
		int named = 0;
		for (var item : seen.entrySet()) {
			var function = getFunctionAt(item.getKey());
			if (function.getSymbol().getSource() != SourceType.DEFAULT) {
				continue; // a hook-signatures.json name or another real name wins
			}
			function.setParentNamespace(owner.get(item.getKey()));
			function.setName("vf" + slotOf.get(item.getKey()), SourceType.ANALYSIS);
			if (item.getValue().size() > 1) {
				setPlateComment(item.getKey(), "In " + item.getValue().size() + " vtables: " +
					String.join(", ", item.getValue().subList(0, Math.min(20, item.getValue().size()))) +
					(item.getValue().size() > 20 ? ", ..." : ""));
			}
			named++;
		}
		println("named " + named + " virtual functions from " + vtables + " vtables");
	}

	private void analyse() throws Exception {
		// Ghidra's RTTI analyzer only searches blocks named .data* (type descriptors) and .rdata* (object locators).
		// This exe's sections have other names, and its 10,719 ".?AV" type names, type_info's among them, lie in
		// .debug's file range (0x2bb59b0-0x2ca10b8; section table read on 2026-11-17, game build 548ddc95). So in the
		// project only, .debug becomes .data.debug and .udata .rdata.udata; the exe is not touched.
		for (String[] rename : new String[][] { { ".debug", ".data.debug" }, { ".udata", ".rdata.udata" } }) {
			MemoryBlock block = currentProgram.getMemory().getBlock(rename[0]);
			if (block != null) {
				block.setName(rename[1]);
			}
		}
		List<MemoryBlock> hidden = new ArrayList<>();
		AddressSet set = new AddressSet();
		for (MemoryBlock block : currentProgram.getMemory().getBlocks()) {
			if (SKIPPED.contains(block.getName())) {
				if (block.isExecute()) {
					block.setExecute(false);
					hidden.add(block);
				}
			}
			else if (block.isInitialized()) {
				set.add(block.getStart(), block.getEnd());
			}
		}
		var options = currentProgram.getOptions(Program.DISASSEMBLER_PROPERTIES);
		boolean restricted =
			options.getBoolean(Disassembler.RESTRICT_DISASSEMBLY_TO_EXECUTE_MEMORY_PROPERTY, false);
		options.setBoolean(Disassembler.RESTRICT_DISASSEMBLY_TO_EXECUTE_MEMORY_PROPERTY, true);
		try {
			// This analyzer runs out of a 16G heap on this exe (AddressTableAnalyzer.removeNonSearchableMemory, Ghidra
			// 12.2, 2026-11-17); RTTI and pointer analysis still find vtables and jump tables come from the decompiler.
			setAnalysisOption(currentProgram, "Create Address Tables", "false");
			AutoAnalysisManager mgr = AutoAnalysisManager.getAnalysisManager(currentProgram);
			mgr.initializeOptions();
			mgr.reAnalyzeAll(set);
			analyzeChanges(currentProgram);
		}
		finally {
			options.setBoolean(Disassembler.RESTRICT_DISASSEMBLY_TO_EXECUTE_MEMORY_PROPERTY, restricted);
			for (MemoryBlock block : hidden) {
				block.setExecute(true);
			}
		}
		println("analysed " + set.getNumAddresses() + " bytes; skipped " + SKIPPED);
	}

	// Each signature that matches exactly once in its section gets its name, as a label and, in analysed code, a
	// function name. A signature with no match or several is reported and left alone.
	private void importSignatures(String path, String build) throws Exception {
		JsonArray entries;
		try (FileReader reader = new FileReader(path)) {
			entries = JsonParser.parseReader(reader).getAsJsonArray();
		}
		int named = 0;
		for (JsonElement element : entries) {
			JsonObject entry = element.getAsJsonObject();
			String name = entry.get("name").getAsString();
			String section = entry.get("section").getAsString();
			MemoryBlock block = currentProgram.getMemory().getBlock(section);
			if (block == null) {
				println("signature " + name + ": no section " + section);
				continue;
			}
			Address match = findOnce(block, entry.get("pattern").getAsString());
			if (match == null) {
				println("signature " + name + ": not exactly one match in " + section);
				continue;
			}
			boolean listed = false;
			for (JsonElement b : entry.getAsJsonArray("gameBuilds")) {
				listed |= b.getAsString().equalsIgnoreCase(build);
			}
			createLabel(match, name, true, SourceType.USER_DEFINED);
			var function = getFunctionAt(match);
			if (function != null) {
				function.setName(name, SourceType.USER_DEFINED);
			}
			setPlateComment(match, "hook-signatures.json: " + name + (listed ? "" : " (build not listed)"));
			named++;
		}
		println("named " + named + " of " + entries.size() + " signatures");
	}

	private Address findOnce(MemoryBlock block, String pattern) {
		String[] tokens = pattern.trim().split("\\s+");
		byte[] bytes = new byte[tokens.length];
		byte[] masks = new byte[tokens.length];
		for (int i = 0; i < tokens.length; i++) {
			if (!tokens[i].equals("??")) {
				bytes[i] = (byte) Integer.parseInt(tokens[i], 16);
				masks[i] = (byte) 0xff;
			}
		}
		var memory = currentProgram.getMemory();
		Address first = memory.findBytes(block.getStart(), block.getEnd(), bytes, masks, true, monitor);
		if (first == null || first.equals(block.getEnd())) {
			return first;
		}
		Address second = memory.findBytes(first.add(1), block.getEnd(), bytes, masks, true, monitor);
		return second == null ? first : null;
	}
}
