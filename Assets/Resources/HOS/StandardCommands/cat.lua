function execute(args)
    if #args == 0 then
        hos.stderr("Usage: cat <file> [...]")
        return 2
    end

    for _, path in ipairs(args) do
        local content, err = hos.fs.read(path)
        if not content then
            hos.stderr("cat: " .. path .. ": " .. err.message)
            return err.code
        end
        hos.stdout(content)
    end

    return 0
end
