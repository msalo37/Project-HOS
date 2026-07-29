function execute(args)
    if #args > 1 then
        hos.stderr("Usage: ls [directory]")
        return 2
    end

    local entries, err = hos.fs.list(args[1] or ".")
    if not entries then
        hos.stderr("ls: " .. err.message)
        return err.code
    end

    for _, entry in ipairs(entries) do
        hos.stdout(entry.name)
    end

    return 0
end
