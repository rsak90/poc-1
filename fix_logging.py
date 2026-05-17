import re

file = r'c:\Users\USER\OneDrive\Desktop\offline\offline-jobs\poc-1\src\KerberosConstrainedDelegation\KerberosTokenManager.cs'

with open(file, 'r', encoding='utf-8') as f:
    content = f.read()

lines = content.split('\n')
out = []

for line in lines:
    # Skip lines that already use _logger. or Log. correctly
    # We need to:
    # 1. Convert Console.WriteLine($"[TokenManager] ...{var}...") -> _logger.Information("[TokenManager] ...", var)
    # 2. Convert Console.WriteLine($"[TokenManager] ERROR...{var}...") -> _logger.Error(...)
    # 3. Convert Console.WriteLine($"[TokenDiag]...") -> _logger.Debug(...) (static method - use Log.Debug)
    # 4. Convert Console.WriteLine($"[DEBUG]...") -> Log.Debug(...)
    # 5. Fix existing _logger calls that use string interpolation -> structured logging

    stripped = line.strip()

    # Fix existing _logger.Information/Error calls that use $"..." interpolation
    # These were already partially converted but use wrong syntax
    def fix_logger_call(m):
        indent = m.group(1)
        level = m.group(2)  # Information or Error
        msg_content = m.group(3)  # content inside the $"..."

        # Extract {variable} placeholders and convert to Serilog structured params
        vars_found = re.findall(r'\{([^}:]+)(?::[^}]*)?\}', msg_content)
        # Replace {var} with {Var} (Serilog convention: PascalCase property names)
        serilog_msg = re.sub(r'\{([^}:]+)(:[^}]*)?\}', lambda x: '{' + x.group(1).replace('.', '_').replace('?', '') + '}', msg_content)
        
        if vars_found:
            params = ', '.join(v.replace('.', '_').replace('?', '') for v in vars_found)
            # Build the actual C# expressions for the params
            csharp_params = ', '.join(v for v in vars_found)
            return f'{indent}_logger.{level}("{serilog_msg}", {csharp_params});'
        else:
            return f'{indent}_logger.{level}("{serilog_msg}");'

    # Match _logger.Information/Error($"...") - already partially converted
    line = re.sub(
        r'^(\s*)_logger\.(Information|Error|Warning|Debug)\(\$"([^"]+)"\);',
        fix_logger_call,
        line
    )

    # Convert Console.WriteLine($"[TokenManager] ERROR...") -> _logger.Error
    def convert_error(m):
        indent = m.group(1)
        msg_content = m.group(2)
        vars_found = re.findall(r'\{([^}:]+)(?::[^}]*)?\}', msg_content)
        serilog_msg = re.sub(r'\{([^}:]+)(:[^}]*)?\}', lambda x: '{' + x.group(1).replace('.', '_').replace('?', '') + '}', msg_content)
        if vars_found:
            csharp_params = ', '.join(v for v in vars_found)
            return f'{indent}_logger.Error("{serilog_msg}", {csharp_params});'
        else:
            return f'{indent}_logger.Error("{serilog_msg}");'

    line = re.sub(
        r'^(\s*)Console\.WriteLine\(\$"(\[TokenManager\] ERROR[^"]+)"\);',
        convert_error,
        line
    )

    # Convert Console.WriteLine($"[TokenManager] ...") -> _logger.Information
    def convert_info(m):
        indent = m.group(1)
        msg_content = m.group(2)
        vars_found = re.findall(r'\{([^}:]+)(?::[^}]*)?\}', msg_content)
        serilog_msg = re.sub(r'\{([^}:]+)(:[^}]*)?\}', lambda x: '{' + x.group(1).replace('.', '_').replace('?', '') + '}', msg_content)
        if vars_found:
            csharp_params = ', '.join(v for v in vars_found)
            return f'{indent}_logger.Information("{serilog_msg}", {csharp_params});'
        else:
            return f'{indent}_logger.Information("{serilog_msg}");'

    line = re.sub(
        r'^(\s*)Console\.WriteLine\(\$"(\[TokenManager\][^"]+)"\);',
        convert_info,
        line
    )

    # Convert Console.WriteLine($"[TokenDiag]...") -> Log.Debug (static method)
    def convert_diag(m):
        indent = m.group(1)
        msg_content = m.group(2)
        vars_found = re.findall(r'\{([^}:]+)(?::[^}]*)?\}', msg_content)
        serilog_msg = re.sub(r'\{([^}:]+)(:[^}]*)?\}', lambda x: '{' + x.group(1).replace('.', '_').replace('?', '') + '}', msg_content)
        if vars_found:
            csharp_params = ', '.join(v for v in vars_found)
            return f'{indent}Log.Debug("{serilog_msg}", {csharp_params});'
        else:
            return f'{indent}Log.Debug("{serilog_msg}");'

    line = re.sub(
        r'^(\s*)Console\.WriteLine\(\$"(\[TokenDiag\][^"]+)"\);',
        convert_diag,
        line
    )

    # Convert Console.WriteLine("[DEBUG] ...") -> Log.Debug (static, no interpolation)
    line = re.sub(
        r'^(\s*)Console\.WriteLine\("(\[DEBUG\][^"]+)"\);',
        lambda m: f'{m.group(1)}Log.Debug("{m.group(2)}");',
        line
    )

    # Convert Console.WriteLine($"[DEBUG] ...") -> Log.Debug
    line = re.sub(
        r'^(\s*)Console\.WriteLine\(\$"(\[DEBUG\][^"]+)"\);',
        convert_diag,
        line
    )

    out.append(line)

result = '\n'.join(out)

# Count remaining Console.WriteLine
remaining = len(re.findall(r'Console\.WriteLine', result))
print(f'Remaining Console.WriteLine: {remaining}')

with open(file, 'w', encoding='utf-8') as f:
    f.write(result)

print('Done')
