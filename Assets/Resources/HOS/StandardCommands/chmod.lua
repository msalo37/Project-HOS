function execute(args)
    if #args ~= 2 then
        hos.stderr("Usage: chmod <mode> <path>")
        return 2
    end

    local ok, err = hos.fs.chmod(args[2], args[1])
    if not ok then
        hos.stderr("chmod: " .. err.message)
        return err.code
    end

    return 0
end
