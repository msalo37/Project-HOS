function execute(args)
    if #args ~= 2 then
        hos.stderr("Usage: cp <source> <destination>")
        return 2
    end

    local ok, err = hos.fs.copy(args[1], args[2])
    if not ok then
        hos.stderr("cp: " .. err.message)
        return err.code
    end

    return 0
end
