function execute(args)
    if #args ~= 1 then
        hos.stderr("Usage: mkdir <directory>")
        return 2
    end

    local created, err = hos.fs.mkdir(args[1])
    if not created then
        hos.stderr("mkdir: " .. err.message)
        return err.code
    end

    return 0
end
