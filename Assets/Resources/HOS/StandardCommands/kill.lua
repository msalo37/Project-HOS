function execute(args)
    if #args ~= 1 then
        hos.stderr("Usage: kill <pid>")
        return 2
    end

    local pid = tonumber(args[1])
    if pid == nil then
        hos.stderr("kill: pid must be a number")
        return 2
    end

    local ok, err = hos.process.kill(pid)
    if not ok then
        hos.stderr("kill: " .. err.message)
        return err.code
    end

    return 0
end
