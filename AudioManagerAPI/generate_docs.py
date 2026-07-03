import os
import re

def generate_markdown_docs(source_dir, output_file):
    """
    Parses C# source files line-by-line to safely extract XML documentation summaries 
    and generates a clean Markdown API registry without regex backtracking anomalies.
    Fully supports multi-line parameter lists, generic type structures, and multi-line expression bodies.
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

                    # 3. Process valid method definitions and bind accumulated summaries
                    if summary_lines and current_class:
                        if ("public" in stripped or "internal" in stripped) and "(" in stripped:
                            full_signature = stripped
                            next_idx = idx + 1
                            
                            # Advanced Lookahead Engine: Aggressively ingest subsequent lines until 
                            # the logical C# language declaration block boundary is reached safely.
                            while next_idx < len(lines):
                                sig_stripped = full_signature.strip()
                                
                                # Case A: If it's an expression-bodied member, it must end with a semicolon
                                if "=>" in sig_stripped:
                                    if sig_stripped.endswith(";"):
                                        break
                                # Case B: Standard methods end when hitting the block opener '{' or semicolon ';'
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
                            
                            # Safely extract the method name token, fully supporting generic layouts <T>
                            name_match = re.search(r'(\w+)\s*(?:<[\w,\s]+>)?\s*\(', method_decl_clean)
                            actual_method_name = name_match.group(1) if name_match else "UnknownMethod"

                            # Sanitize trailing syntax artifacts for clean markdown display
                            if method_decl_clean.endswith("{"):
                                method_decl_clean = method_decl_clean[:-1].strip()

                            markdown_content += f"### 🔹 `{actual_method_name}()`\n"
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