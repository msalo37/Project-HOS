function execute(args)
    if #args ~= 2 then
        hos.stderr("Usage: mv <source> <destination>")
        return 2
    end

    local ok, err = hos.fs.move(args[1], args[2])
    if not ok then
        hos.stderr("mv: " .. err.message)
        return err.code
    end

    return 0
end
