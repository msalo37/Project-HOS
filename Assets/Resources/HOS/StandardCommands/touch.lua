function execute(args)
    if #args ~= 1 then
        hos.stderr("Usage: touch <file>")
        return 2
    end

    if hos.fs.exists(args[1]) then
        return 0
    end

    local created, err = hos.fs.create(args[1])
    if not created then
        hos.stderr("touch: " .. err.message)
        return err.code
    end

    return 0
end
