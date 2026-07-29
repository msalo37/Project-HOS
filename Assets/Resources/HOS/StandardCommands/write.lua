function execute(args)
    if #args < 2 then
        hos.stderr("Usage: write <file> <content>")
        return 2
    end

    local parts = {}
    for i = 2, #args do
        table.insert(parts, args[i])
    end

    local ok, err = hos.fs.write(args[1], table.concat(parts, " "))
    if not ok then
        hos.stderr("write: " .. err.message)
        return err.code
    end

    return 0
end
