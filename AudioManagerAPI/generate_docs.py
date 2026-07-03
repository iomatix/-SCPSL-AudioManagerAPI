import os
import re

def generate_markdown_docs(source_dir, output_file):
    """
    Parses C# source files line-by-line to safely extract XML documentation summaries 
    and generates a clean Markdown API registry without regex backtracking anomalies.
    Fully supports multi-line parameter lists, generic type structures, properties, fields, and tuples.
    """
    markdown_content = "# Audio Manager API - Architecture API Registry\n\n"
    print("Initializing source code scanning architecture...")

    # Strict directory exclusion matrix to optimize execution speed and skip build artifacts
    EXCLUDED_DIRS = {'.git', 'bin', 'obj', '.vs', 'packages'}
    file_count = 0

    for root, dirs, files in os.walk(source_dir):
        # Prune excluded directories in-place to prevent scanning overhead
        dirs[:] = [d for d in dirs if d not in EXCLUDED_DIRS]

        for file in sorted(files):
            if file.endswith('.cs') and not file.startswith('AssemblyInfo'):
                file_path = os.path.join(root, file)
                print(f"Processing structural asset: {file}")
                file_count += 1
                
                with open(file_path, 'r', encoding='utf-8') as f:
                    lines = f.readlines()

                current_class = None
                summary_lines = []
                in_summary_block = False

                idx = 0
                while idx < len(lines):
                    stripped = lines[idx].strip()

                    # 1. Detect concrete class definition boundaries
                    class_match = re.search(r'(?:public|internal)\s+(?:static\s+)?class\s+(\w+)', stripped)
                    if class_match:
                        current_class = class_match.group(1)
                        markdown_content += f"## 📦 Class: {current_class}\n\n"
                        summary_lines = []
                        idx += 1
                        continue

                    # 2. Extract and accumulate XML documentation chunks
                    if stripped.startswith("///"):
                        xml_content = stripped[3:].strip()
                        
                        if "<summary>" in xml_content:
                            in_summary_block = True
                            xml_content = xml_content.replace("<summary>", "")
                        
                        if "</summary>" in xml_content:
                            in_summary_block = False
                            xml_content = xml_content.replace("</summary>", "")
                            
                        if in_summary_block:
                            if xml_content:
                                summary_lines.append(xml_content)
                        else:
                            if xml_content and not any(tag in xml_content for tag in ["<summary>", "</summary>", "<param", "<returns"]):
                                summary_lines.append(xml_content)
                        idx += 1
                        continue

                    # Skip preprocessor directives, system annotations, and structural braces
                    if not stripped or stripped.startswith("[") or stripped.startswith("//") or stripped.startswith("#") or stripped in ("{", "}"):
                        idx += 1
                        continue

                    # 3. Process valid member definitions (Methods, Properties, Fields) and bind summaries
                    if summary_lines and current_class:
                        if stripped.startswith("public ") or stripped.startswith("internal "):
                            full_signature = stripped
                            next_idx = idx + 1
                            
                            # Advanced Lookahead Engine: Aggressively ingest subsequent lines until 
                            # the logical C# language declaration block boundary is reached safely.
                            while next_idx < len(lines):
                                sig_stripped = full_signature.strip()
                                
                                # Terminate accumulation if a structural language boundary is hit
                                if "=>" in sig_stripped:
                                    if sig_stripped.endswith(";"):
                                        break
                                else:
                                    if "{" in sig_stripped or ";" in sig_stripped:
                                        break
                                        
                                next_stripped = lines[next_idx].strip()
                                if next_stripped:
                                    full_signature += " " + next_stripped
                                next_idx += 1
                                
                            # Synchronize the master state iterator with the lookahead terminal index
                            idx = next_idx - 1

                            raw_summary = " ".join(summary_lines).strip()
                            method_decl_clean = re.sub(r'\s+', ' ', full_signature).strip()
                            
                            # Determine member token type based on the first structural character encountered
                            first_char = None
                            first_pos = len(method_decl_clean)
                            for char in ['(', '{', ';', '=>']:
                                pos = method_decl_clean.find(char)
                                if pos != -1 and pos < first_pos:
                                    first_pos = pos
                                    first_char = char

                            is_method = (first_char == '(')

                            # Cut off code at the first structural boundary to isolate type and identifier name safely
                            declaration_core = method_decl_clean[:first_pos].strip()

                            # Extract the member identifier name, handling potential trailing generic method blocks <T>
                            name_match = re.search(r'(\b\w+)\s*(?:<[^>]+>)?$', declaration_core)
                            actual_member_name = name_match.group(1) if name_match else "UnknownMember"

                            # Sanitize trailing syntax artifacts for clean markdown display
                            if method_decl_clean.endswith("{"):
                                method_decl_clean = method_decl_clean[:-1].strip()

                            # Render the Markdown document node based on explicit structural type rules
                            if is_method:
                                markdown_content += f"### 🔹 `{actual_member_name}()`\n"
                            else:
                                markdown_content += f"### 🔹 `{actual_member_name}`\n"

                            markdown_content += f"**Description:** {raw_summary}\n"
                            markdown_content += f"```csharp\n{method_decl_clean}\n```\n\n"
                        
                        # Always flush memory buffer state tracking when hitting a real code node
                        summary_lines = []

                    idx += 1

                if current_class:
                    markdown_content += "---\n\n"

    with open(output_file, 'w', encoding='utf-8') as out:
        out.write(markdown_content)

    print(f"\nSuccess! Structural analysis complete. Analyzed {file_count} files.")
    print(f"Documentation registry exported to: {output_file}")


if __name__ == "__main__":
    generate_markdown_docs("./", "DOCUMENTATION.md")