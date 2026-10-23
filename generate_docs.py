import os
import re
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Tuple


@dataclass
class DocComment:
    """Stores structured data extracted from a C# XML documentation comment."""
    summary: str = ""
    remarks: str = ""
    returns: str = ""
    params: Dict[str, str] = field(default_factory=dict)
    typeparams: Dict[str, str] = field(default_factory=dict)

    def is_empty(self) -> bool:
        return not (self.summary or self.remarks or self.returns or self.params or self.typeparams)


@dataclass
class MemberDoc:
    """Represents a documented type member (method, property, field, or constant)."""
    name: str
    is_method: bool
    signature: str
    doc: DocComment


@dataclass
class TypeDoc:
    """Represents a documented C# type (class, struct, interface, enum)."""
    kind: str
    name: str
    doc: DocComment
    members: List[MemberDoc] = field(default_factory=list)


# Precompiled regular expressions for performance and determinism
TYPE_DECL_REGEX = re.compile(
    r'^(?:public|internal)\s+'
    r'(?:(?:static|sealed|abstract|readonly|partial)\s+)*'
    r'(class|struct|interface|enum)\s+'
    r'([A-Za-z_]\w*(?:<[^>]+>)?)'
)

IDENTIFIER_REGEX = re.compile(r'([A-Za-z_]\w*)\s*(?:<[^>]+>)?$')


def clean_xml_tags(text: str) -> str:
    """Converts C# documentation XML tags into native Markdown formatting."""
    if not text:
        return ""

    # <see cref="Namespace.Type.Member"/> or <see cref="Member">Text</see>
    def replace_cref(match: re.Match) -> str:
        cref = match.group(1).strip()
        # Strip documentation type prefix (e.g., M:, T:, P:, F:) if present
        if len(cref) > 2 and cref[1] == ':':
            cref = cref[2:]
        # Convert XML generic notation {T} to C# notation <T>
        cref = cref.replace('{', '<').replace('}', '>')
        return f"`{cref}`"

    text = re.sub(r'<see\s+cref="([^"]+)"\s*/>', replace_cref, text)
    text = re.sub(r'<see\s+cref="([^"]+)"\s*>(.*?)</see>', lambda m: f"`{m.group(2) or m.group(1)}`", text)
    text = re.sub(r'<seealso\s+cref="([^"]+)"\s*/>', replace_cref, text)

    # <see langword="true"/> -> `true`
    text = re.sub(r'<see\s+langword="([^"]+)"\s*/>', r'`\1`', text)

    # <paramref name="x"/> and <typeparamref name="T"/> -> `x` / `T`
    text = re.sub(r'<(?: type)?paramref\s+name="([^"]+)"\s*/>', r'`\1`', text)
    text = re.sub(r'<paramref\s+name="([^"]+)"\s*/>', r'`\1`', text)
    text = re.sub(r'<typeparamref\s+name="([^"]+)"\s*/>', r'`\1`', text)

    # <c>code</c> and <code>code</code> -> `code`
    text = re.sub(r'<(?:c|code)>(.*?)</(?:c|code)>', r'`\1`', text)

    # Remove remaining raw XML tags (e.g., <para>, </para>, <example>, </returns>)
    text = re.sub(r'</?[a-zA-Z][^>]*>', '', text)

    # Normalize whitespace
    return re.sub(r'\s+', ' ', text).strip()


def parse_xml_block(xml_lines: List[str]) -> DocComment:
    """Parses a set of /// documentation comment lines into a DocComment structure."""
    raw_xml = " ".join(xml_lines)
    doc = DocComment()

    # Extract <summary>
    summary_match = re.search(r'<summary>(.*?)</summary>', raw_xml, re.DOTALL)
    if summary_match:
        doc.summary = clean_xml_tags(summary_match.group(1))
    else:
        # Fallback for comments without an explicit <summary> tag
        stripped = re.sub(
            r'<(?:remarks|returns|param|typeparam|example|exception|inheritdoc)[^>]*>.*?</(?:remarks|returns|param|typeparam|example|exception)>',
            '',
            raw_xml,
            flags=re.DOTALL
        )
        stripped = re.sub(r'<inheritdoc\s*/>', '', stripped)
        doc.summary = clean_xml_tags(stripped)

    # Extract <remarks>
    remarks_match = re.search(r'<remarks>(.*?)</remarks>', raw_xml, re.DOTALL)
    if remarks_match:
        doc.remarks = clean_xml_tags(remarks_match.group(1))

    # Extract <returns>
    returns_match = re.search(r'<returns>(.*?)</returns>', raw_xml, re.DOTALL)
    if returns_match:
        doc.returns = clean_xml_tags(returns_match.group(1))

    # Extract <typeparam name="...">
    for tp_match in re.finditer(r'<typeparam\s+name="([^"]+)"\s*>(.*?)</typeparam>', raw_xml, re.DOTALL):
        tp_name = tp_match.group(1).strip()
        tp_desc = clean_xml_tags(tp_match.group(2))
        if tp_name and tp_desc:
            doc.typeparams[tp_name] = tp_desc

    # Extract <param name="...">
    for p_match in re.finditer(r'<param\s+name="([^"]+)"\s*>(.*?)</param>', raw_xml, re.DOTALL):
        p_name = p_match.group(1).strip()
        p_desc = clean_xml_tags(p_match.group(2))
        if p_name and p_desc:
            doc.params[p_name] = p_desc

    return doc


def find_first_structural_boundary(signature: str) -> Tuple[int, Optional[str]]:
    """
    Identifies the first syntax boundary of a C# declaration outside generic brackets <...>.
    Handles: '(', '{', ';', '=>', and single '=' (field/constant value assignment).
    """
    depth_angle = 0
    length = len(signature)
    i = 0

    while i < length:
        ch = signature[i]
        if ch == '<':
            depth_angle += 1
        elif ch == '>':
            if i > 0 and signature[i - 1] == '=':
                pass
            else:
                depth_angle = max(0, depth_angle - 1)
        elif depth_angle == 0:
            if signature.startswith("=>", i):
                return i, "=>"
            if ch == '(':
                return i, "("
            if ch == '{':
                return i, "{"
            if ch == ';':
                return i, ";"
            if ch == '=' and (i + 1 >= length or signature[i + 1] != '>'):
                return i, "="
        i += 1

    return length, None


def extract_member_metadata(full_signature: str) -> Tuple[str, bool, str]:
    """
    Returns a tuple of (member_name, is_method, clean_signature).
    Safely handles constants, readonly fields with casts, properties, and generic methods.
    """
    clean_sig = re.sub(r'\s+', ' ', full_signature).strip()
    if clean_sig.endswith("{"):
        clean_sig = clean_sig[:-1].strip()

    pos, token = find_first_structural_boundary(clean_sig)
    is_method = (token == "(")
    declaration_core = clean_sig[:pos].strip()

    match = IDENTIFIER_REGEX.search(declaration_core)
    member_name = match.group(1) if match else "UnknownMember"

    return member_name, is_method, clean_sig


def resolve_project_title(source_dir: str, fallback_title: Optional[str]) -> str:
    """Detects project title based on a .csproj file or root directory name."""
    if fallback_title:
        return fallback_title

    for root, dirs, files in os.walk(source_dir):
        dirs[:] = [d for d in dirs if d not in {'.git', 'bin', 'obj', '.vs', 'packages'}]
        for file in files:
            if file.endswith('.csproj'):
                project_name = os.path.splitext(file)[0]
                return f"{project_name} - Architecture API Registry"

    folder_name = os.path.basename(os.path.abspath(source_dir))
    return f"{folder_name or 'Project'} - Architecture API Registry"


def generate_markdown_docs(source_dir: str = "./", output_file: str = "DOCUMENTATION.md", title: Optional[str] = None) -> None:
    """
    Scans C# source files in the specified directory and generates a complete API registry in Markdown format.
    Skips empty classes and inner <inheritdoc/> tags without descriptions.
    """
    resolved_title = resolve_project_title(source_dir, title)
    print(f"Initializing source code scanning architecture for: {resolved_title}...")

    excluded_dirs = {'.git', 'bin', 'obj', '.vs', 'packages'}
    file_count = 0
    documented_types: List[TypeDoc] = []

    for root, dirs, files in os.walk(source_dir):
        dirs[:] = [d for d in dirs if d not in excluded_dirs]

        for file in sorted(files):
            if not file.endswith('.cs') or file.startswith('AssemblyInfo') or file.endswith('.AssemblyAttributes.cs'):
                continue

            file_path = os.path.join(root, file)
            print(f"Processing structural asset: {file}")
            file_count += 1

            with open(file_path, 'r', encoding='utf-8') as f:
                lines = f.readlines()

            current_type: Optional[TypeDoc] = None
            xml_buffer: List[str] = []

            idx = 0
            total_lines = len(lines)

            while idx < total_lines:
                stripped = lines[idx].strip()

                # 1. Accumulate XML documentation comments (///)
                if stripped.startswith("///"):
                    xml_buffer.append(stripped[3:].strip())
                    idx += 1
                    continue

                # Skip blank lines, attributes [...], #region directives, and regular comments //
                if (
                    not stripped
                    or stripped.startswith("[")
                    or stripped.startswith("//")
                    or stripped.startswith("#")
                    or stripped in ("{", "}")
                ):
                    idx += 1
                    continue

                # 2. Detect type declaration (class, struct, interface, enum)
                type_match = TYPE_DECL_REGEX.match(stripped)
                if type_match:
                    type_kind = type_match.group(1).capitalize()
                    type_name = type_match.group(2)
                    type_doc = parse_xml_block(xml_buffer) if xml_buffer else DocComment()
                    xml_buffer = []

                    current_type = TypeDoc(kind=type_kind, name=type_name, doc=type_doc)
                    documented_types.append(current_type)
                    idx += 1
                    continue

                # 3. Detect members inside the active type
                if xml_buffer and current_type is not None:
                    if stripped.startswith("public ") or stripped.startswith("internal "):
                        full_signature = stripped
                        next_idx = idx + 1

                        # Multi-line lookahead: collect full signature up to C# syntax boundary
                        while next_idx < total_lines:
                            sig_stripped = full_signature.strip()
                            if "=>" in sig_stripped:
                                if sig_stripped.endswith(";"):
                                    break
                            else:
                                if "{" in sig_stripped or ";" in sig_stripped:
                                    break

                            next_line = lines[next_idx].strip()
                            if next_line and not next_line.startswith("//"):
                                full_signature += " " + next_line
                            next_idx += 1

                        idx = next_idx - 1
                        member_doc = parse_xml_block(xml_buffer)

                        if not member_doc.is_empty():
                            member_name, is_method, clean_sig = extract_member_metadata(full_signature)
                            current_type.members.append(
                                MemberDoc(
                                    name=member_name,
                                    is_method=is_method,
                                    signature=clean_sig,
                                    doc=member_doc
                                )
                            )

                    xml_buffer = []
                else:
                    xml_buffer = []

                idx += 1

    # Render output Markdown document (non-empty types only)
    md_lines: List[str] = [f"# {resolved_title}\n"]

    for t_doc in documented_types:
        if not t_doc.members and t_doc.doc.is_empty():
            continue

        md_lines.append(f"## 📦 {t_doc.kind}: `{t_doc.name}`\n")
        if t_doc.doc.summary:
            md_lines.append(f"{t_doc.doc.summary}\n")
        if t_doc.doc.remarks:
            md_lines.append(f"> **Note:** {t_doc.doc.remarks}\n")

        for m in t_doc.members:
            header_suffix = "()" if m.is_method else ""
            md_lines.append(f"### 🔹 `{m.name}{header_suffix}`")

            if m.doc.summary:
                md_lines.append(f"**Description:** {m.doc.summary}")

            if m.doc.remarks:
                md_lines.append(f"\n> **Remarks:** {m.doc.remarks}")

            md_lines.append(f"```csharp\n{m.signature}\n```")

            if m.doc.typeparams:
                md_lines.append("**Type Parameters:**")
                for tp_name, tp_desc in m.doc.typeparams.items():
                    md_lines.append(f"- `{tp_name}`: {tp_desc}")

            if m.doc.params:
                md_lines.append("**Parameters:**")
                for p_name, p_desc in m.doc.params.items():
                    md_lines.append(f"- `{p_name}`: {p_desc}")

            if m.doc.returns:
                md_lines.append(f"**Returns:** {m.doc.returns}")

            md_lines.append("")

        md_lines.append("---\n")

    with open(output_file, 'w', encoding='utf-8') as out:
        out.write("\n".join(md_lines))

    print(f"\nSuccess! Structural analysis complete. Analyzed {file_count} files.")
    print(f"Documentation registry exported to: {output_file}")


if __name__ == "__main__":
    generate_markdown_docs("./", "DOCUMENTATION.md")