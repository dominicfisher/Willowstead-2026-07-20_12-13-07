src = open('Assets/Scripts/World/ProceduralGridGenerator.cs').read()
lines = src.split('\n')
depth = 0
in_string = False
in_block_comment = False
last_zero = -1
zero_events = []
for i, ln in enumerate(lines, 1):
    stripped = ln.lstrip()
    # Skip lines that are pure /// XML doc comments — Roslyn consumes the entire line.
    if not in_string and stripped.startswith('///'):
        continue
    j = 0
    while j < len(ln):
        ch = ln[j]
        nxt = ln[j+1] if j+1 < len(ln) else ''
        if in_block_comment:
            if ch == '*' and nxt == '/':
                in_block_comment = False
                j += 2
                continue
            j += 1
            continue
        if ch == '/' and nxt == '*':
            in_block_comment = True
            j += 2
            continue
        if ch == '/' and nxt == '/':
            break  # rest of line is line comment
        if ch == '"':
            in_string = not in_string
            j += 1
            continue
        if not in_string:
            if ch == '{':
                depth += 1
            elif ch == '}':
                depth -= 1
                if depth < 0:
                    print(f'NEGATIVE DEPTH at line {i}: {ln[:80]!r}')
                    sys.exit(1) if False else None
                    break
        j += 1
    if depth == 0 and i not in (1, len(lines)):
        zero_events.append(i)
print(f'Total lines: {len(lines)}')
print(f'Final depth: {depth}')
print(f'Lines where depth = 0 (excluding line 1 and EOF): {len(zero_events)}')
if zero_events:
    print(f'First 10: {zero_events[:10]}')
    print(f'Last 10:  {zero_events[-10:]}')
