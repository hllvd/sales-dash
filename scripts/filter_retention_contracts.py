#!/usr/bin/env python3
"""
Filter Retention Contracts (Modelo de Retenção)
==============================================
Filters rows in Model A (.xlsx or .csv) based on contract numbers present in Model B (.xlsx or .csv),
generating Model C (.xlsx or .csv) containing only the matching rows.

Preserves 100% of Model A's column structure, headers, and row data.

Contract Decomposition Logic:
- Handles composed column formats like '012173;4103;0;MARIO;1100326334' -> '1100326334'
- Trims whitespace and strips leading zeros for canonical comparison.

Usage:
    python3 filter_retention_contracts.py <model_a.xlsx> <model_b.xlsx> [-o output_c.xlsx]
"""

import argparse
import csv
import io
import os
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
from typing import List, Set, Tuple, Optional, Dict, Any


def normalize_number(value: Optional[str]) -> str:
    """
    Pure function: Strips leading zeros and trims whitespace.
    Mirrors SalesApp.Utils.NormalizationUtils.NormalizeNumber.
    """
    if value is None:
        return ""
    trimmed = str(value).strip()
    if not trimmed:
        return ""
    normalized = trimmed.lstrip("0")
    if len(normalized) == 0 and len(trimmed) > 0:
        return "0"
    return normalized


def decompose_contract(raw_value: Optional[str]) -> str:
    """
    Pure function: Decomposes a potentially concatenated string into the contract number.
    Mirrors SalesApp.Libs.CotaDecomposer.Decompose.
    E.g. '012173;4103;0;MARIO;1100326334' -> '1100326334'
    """
    if not raw_value:
        return ""
    val_str = str(raw_value).strip()
    if ";" in val_str:
        parts = [p.strip() for p in val_str.split(";")]
        # Contract is the last component
        return normalize_number(parts[-1])
    return normalize_number(val_str)


# ---------------------------------------------------------------------------
# Pure XML / Zip XLSX Reader (Standard Library - No dependencies required)
# ---------------------------------------------------------------------------

def read_xlsx_rows(file_path: str) -> List[List[str]]:
    """Reads all rows from the first sheet of an XLSX file using standard library zipfile/xml."""
    with zipfile.ZipFile(file_path, 'r') as z:
        # 1. Parse Shared Strings
        shared_strings: List[str] = []
        if 'xl/sharedStrings.xml' in z.namelist():
            tree = ET.fromstring(z.read('xl/sharedStrings.xml'))
            for si in tree.findall('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}si'):
                # Can contain multiple <t> inside <r>
                text_parts = []
                for t in si.findall('.//{http://schemas.openxmlformats.org/spreadsheetml/2006/main}t'):
                    if t.text:
                        text_parts.append(t.text)
                shared_strings.append("".join(text_parts))

        # 2. Locate first sheet
        sheet_path = 'xl/worksheets/sheet1.xml'
        if sheet_path not in z.namelist():
            # Find first matching sheet
            for name in z.namelist():
                if name.startswith('xl/worksheets/sheet') and name.endswith('.xml'):
                    sheet_path = name
                    break

        sheet_tree = ET.fromstring(z.read(sheet_path))
        rows_data: List[List[str]] = []

        for row_elem in sheet_tree.findall('.//{http://schemas.openxmlformats.org/spreadsheetml/2006/main}row'):
            cells = row_elem.findall('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}c')
            if not cells:
                continue

            row_cells: Dict[int, str] = {}
            max_col_idx = 0

            for c in cells:
                r_attr = c.attrib.get('r', '')  # e.g., 'A1', 'B2'
                col_idx = 0
                if r_attr:
                    col_letters = "".join([ch for ch in r_attr if ch.isalpha()])
                    for char in col_letters:
                        col_idx = col_idx * 26 + (ord(char.upper()) - ord('A') + 1)
                    col_idx -= 1  # 0-indexed
                else:
                    col_idx = max_col_idx

                cell_type = c.attrib.get('t', '')
                val_elem = c.find('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}v')
                cell_val = val_elem.text if val_elem is not None and val_elem.text is not None else ''

                if cell_type == 's' and cell_val.isdigit():
                    idx = int(cell_val)
                    if 0 <= idx < len(shared_strings):
                        cell_val = shared_strings[idx]
                elif cell_type == 'inlineStr':
                    is_elem = c.find('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}is/{http://schemas.openxmlformats.org/spreadsheetml/2006/main}t')
                    if is_elem is not None and is_elem.text:
                        cell_val = is_elem.text

                row_cells[col_idx] = cell_val
                if col_idx > max_col_idx:
                    max_col_idx = col_idx

            # Build row array preserving column positions
            row_list = [row_cells.get(i, '') for i in range(max_col_idx + 1)]
            rows_data.append(row_list)

        return rows_data


def read_csv_rows(file_path: str) -> List[List[str]]:
    """Reads all rows from a CSV file (supports comma, semicolon, tab)."""
    with open(file_path, 'r', encoding='utf-8', errors='replace') as f:
        sample = f.read(4096)
        f.seek(0)
        try:
            dialect = csv.Sniffer().sniff(sample)
        except Exception:
            dialect = csv.excel
            dialect.delimiter = ';' if ';' in sample else ','
        reader = csv.reader(f, dialect)
        return [row for row in reader]


def read_tabular_file(file_path: str) -> List[List[str]]:
    """Dispatches reader based on file extension."""
    if not os.path.exists(file_path):
        raise FileNotFoundError(f"Arquivo não encontrado: {file_path}")
    ext = os.path.splitext(file_path)[1].lower()
    if ext == '.xlsx':
        return read_xlsx_rows(file_path)
    elif ext in ('.csv', '.txt'):
        return read_csv_rows(file_path)
    else:
        raise ValueError(f"Extensão de arquivo não suportada: '{ext}'. Use .xlsx ou .csv.")


# ---------------------------------------------------------------------------
# Pure XML / Zip XLSX Writer (Standard Library - No dependencies required)
# ---------------------------------------------------------------------------

def escape_xml(s: str) -> str:
    """Escapes special XML characters."""
    return (s.replace("&", "&amp;")
             .replace("<", "&lt;")
             .replace(">", "&gt;")
             .replace('"', "&quot;")
             .replace("'", "&apos;"))


def col_index_to_name(col_idx: int) -> str:
    """Converts 0-based column index to Excel column name (0 -> 'A', 27 -> 'AB')."""
    name = ""
    col_idx += 1
    while col_idx > 0:
        col_idx, remainder = divmod(col_idx - 1, 26)
        name = chr(65 + remainder) + name
    return name


def write_xlsx_file(file_path: str, rows: List[List[str]]) -> None:
    """Writes rows to an XLSX file using standard library zipfile and OpenXML format."""
    # Collect unique strings for sharedStrings.xml
    shared_strings_map: Dict[str, int] = {}
    shared_strings_list: List[str] = []

    def get_string_idx(text: str) -> int:
        if text not in shared_strings_map:
            idx = len(shared_strings_list)
            shared_strings_map[text] = idx
            shared_strings_list.append(text)
            return idx
        return shared_strings_map[text]

    # Build sheet XML
    sheet_lines = [
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>',
        '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">',
        '  <sheetData>'
    ]

    for r_idx, row in enumerate(rows, start=1):
        if not any(str(c).strip() for c in row):
            continue
        sheet_lines.append(f'    <row r="{r_idx}">')
        for c_idx, val in enumerate(row):
            str_val = str(val) if val is not None else ""
            if not str_val:
                continue
            col_name = col_index_to_name(c_idx)
            cell_ref = f"{col_name}{r_idx}"
            s_idx = get_string_idx(str_val)
            sheet_lines.append(f'      <c r="{cell_ref}" t="s"><v>{s_idx}</v></c>')
        sheet_lines.append('    </row>')

    sheet_lines.append('  </sheetData>')
    sheet_lines.append('</worksheet>')
    sheet_xml = "\n".join(sheet_lines)

    # Build sharedStrings XML
    ss_lines = [
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>',
        f'<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="{len(shared_strings_list)}" uniqueCount="{len(shared_strings_list)}">'
    ]
    for s in shared_strings_list:
        ss_lines.append(f'  <si><t>{escape_xml(s)}</t></si>')
    ss_lines.append('</sst>')
    ss_xml = "\n".join(ss_lines)

    content_types_xml = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
  <Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/>
  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
</Types>"""

    workbook_xml = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets>
    <sheet name="Retenção Filtrada" sheetId="1" r:id="rId1"/>
  </sheets>
</workbook>"""

    workbook_rels_xml = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
</Relationships>"""

    root_rels_xml = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>"""

    styles_xml = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <fonts count="1"><font><name val="Calibri"/><sz val="11"/></font></fonts>
  <fills count="1"><fill><patternFill patternType="none"/></fill></fills>
  <borders count="1"><border><left/><right/><top/><bottom/></border></borders>
  <cellStyleXfs count="1"><xf/></cellStyleXfs>
  <cellXfs count="1"><xf/></cellXfs>
</styleSheet>"""

    os.makedirs(os.path.dirname(os.path.abspath(file_path)), exist_ok=True)
    with zipfile.ZipFile(file_path, 'w', compression=zipfile.ZIP_DEFLATED) as z:
        z.writestr('[Content_Types].xml', content_types_xml)
        z.writestr('_rels/.rels', root_rels_xml)
        z.writestr('xl/workbook.xml', workbook_xml)
        z.writestr('xl/_rels/workbook.xml.rels', workbook_rels_xml)
        z.writestr('xl/styles.xml', styles_xml)
        z.writestr('xl/sharedStrings.xml', ss_xml)
        z.writestr('xl/worksheets/sheet1.xml', sheet_xml)


def write_csv_file(file_path: str, rows: List[List[str]]) -> None:
    """Writes rows to a CSV file."""
    os.makedirs(os.path.dirname(os.path.abspath(file_path)), exist_ok=True)
    with open(file_path, 'w', newline='', encoding='utf-8-sig') as f:
        writer = csv.writer(f, delimiter=';')
        writer.writerows(rows)


# ---------------------------------------------------------------------------
# Pure Core Filter Logic
# ---------------------------------------------------------------------------

def extract_contract_numbers_from_b(rows_b: List[List[str]]) -> Set[str]:
    """
    Pure function: Extracts all normalized contract numbers from Model B.
    Scans the primary column and ignores empty values and known non-numeric headers.
    """
    contracts: Set[str] = set()
    for row_idx, row in enumerate(rows_b):
        if not row:
            continue
        first_cell = str(row[0]).strip()
        if not first_cell:
            continue

        # Skip header if it contains text description like 'contrato', 'contract', 'número'
        if row_idx == 0 and any(h in first_cell.lower() for h in ['contrat', 'proposta', 'número', 'numero', 'codigo', 'código', 'id']):
            continue

        normalized = normalize_number(first_cell)
        if normalized:
            contracts.add(normalized)

    return contracts


def filter_model_a(rows_a: List[List[str]], contracts_b: Set[str]) -> Tuple[List[List[str]], Dict[str, Any]]:
    """
    Pure function: Filters rows of Model A using the contract set B.
    Returns (filtered_rows_c, stats_dict).
    """
    if not rows_a:
        return [], {
            "total_rows_a": 0,
            "total_contracts_b": len(contracts_b),
            "matched_rows_c": 0,
            "removed_rows": 0,
            "retention_rate": 0.0
        }

    header = rows_a[0]
    matched_rows: List[List[str]] = [header]
    total_data_rows_a = 0
    removed_count = 0

    for row in rows_a[1:]:
        if not row or not any(str(c).strip() for c in row):
            continue
        total_data_rows_a += 1

        first_cell = str(row[0]).strip() if len(row) > 0 else ""
        contract_number = decompose_contract(first_cell)

        # Also fallback to searching columns named 'Contrato' or 'Contract' if first column wasn't composed
        if not contract_number and len(row) > 1:
            for idx, col_val in enumerate(row):
                header_name = str(header[idx]).lower() if idx < len(header) else ""
                if 'contrat' in header_name:
                    contract_number = decompose_contract(str(col_val))
                    if contract_number:
                        break

        if contract_number and contract_number in contracts_b:
            matched_rows.append(row)
        else:
            removed_count += 1

    matched_count = len(matched_rows) - 1
    retention_rate = (matched_count / total_data_rows_a * 100) if total_data_rows_a > 0 else 0.0

    stats = {
        "total_rows_a": total_data_rows_a,
        "total_contracts_b": len(contracts_b),
        "matched_rows_c": matched_count,
        "removed_rows": removed_count,
        "retention_rate": round(retention_rate, 2)
    }

    return matched_rows, stats


# ---------------------------------------------------------------------------
# CLI Entry Point
# ---------------------------------------------------------------------------

def main() -> int:
    parser = argparse.ArgumentParser(
        description="Filtra a planilha Modelo de Retenção (Modelo A) mantendo apenas os contratos presentes no Modelo B.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Exemplos de uso:
  python3 filter_retention_contracts.py modelo_retencao.xlsx lista_contratos.xlsx
  python3 filter_retention_contracts.py modelo_retencao.xlsx lista_contratos.xlsx -o saida_filtrada.xlsx
        """
    )
    parser.add_argument("model_a", help="Caminho do arquivo Modelo A (Base de Retenção .xlsx ou .csv)")
    parser.add_argument("model_b", help="Caminho do arquivo Modelo B (Lista de Contratos .xlsx ou .csv)")
    parser.add_argument("-o", "--output", help="Caminho do arquivo Modelo C gerado (padrão: modelo_retencao_filtrado.xlsx)", default=None)

    args = parser.parse_args()

    # Determine default output path
    output_path = args.output
    if not output_path:
        base_dir = os.path.dirname(args.model_a) or "."
        output_path = os.path.join(base_dir, "modelo_retencao_filtrado.xlsx")

    print("=" * 60)
    print("  FILTRO DE CONTRATOS - MODELO DE RETENÇÃO")
    print("=" * 60)
    print(f"[*] Modelo A (Base):      {args.model_a}")
    print(f"[*] Modelo B (Filtro):    {args.model_b}")
    print(f"[*] Modelo C (Saída):     {output_path}")
    print("-" * 60)

    try:
        # Step 1: Read Model B
        print("[1/4] Lendo contratos do Modelo B...")
        rows_b = read_tabular_file(args.model_b)
        contracts_b = extract_contract_numbers_from_b(rows_b)
        print(f"      -> {len(contracts_b)} contratos únicos identificados no Modelo B.")

        # Step 2: Read Model A
        print("[2/4] Lendo base de retenção do Modelo A...")
        rows_a = read_tabular_file(args.model_a)
        print(f"      -> {max(0, len(rows_a) - 1)} linhas de dados encontradas no Modelo A.")

        # Step 3: Pure Filtering
        print("[3/4] Aplicando filtro e decomposição de contratos...")
        filtered_rows_c, stats = filter_model_a(rows_a, contracts_b)

        # Step 4: Write Output File C
        print(f"[4/4] Gravando arquivo de saída Modelo C ({output_path})...")
        ext = os.path.splitext(output_path)[1].lower()
        if ext == '.csv':
            write_csv_file(output_path, filtered_rows_c)
        else:
            write_xlsx_file(output_path, filtered_rows_c)

        print("-" * 60)
        print("  RESULTADO DO PROCESSAMENTO:")
        print(f"  - Total de linhas na Base (A):       {stats['total_rows_a']}")
        print(f"  - Contratos únicos no Filtro (B):   {stats['total_contracts_b']}")
        print(f"  - Linhas mantidas no Modelo C:      {stats['matched_rows_c']}")
        print(f"  - Linhas descartadas:               {stats['removed_rows']}")
        print(f"  - Taxa de retenção:                 {stats['retention_rate']}%")
        print("=" * 60)
        print(f"[✔] Arquivo gerado com sucesso em: {os.path.abspath(output_path)}\n")
        return 0

    except Exception as ex:
        print(f"\n[✖] ERRO DURANTE A EXECUÇÃO: {ex}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
