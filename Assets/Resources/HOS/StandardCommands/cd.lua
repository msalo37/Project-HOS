function execute(args)
    if #args > 1 then
        hos.stderr("Usage: cd [directory]")
        return 2
    end

    local path = args[1] or hos.shell.getenv("HOME") or "/"
    local ok, err = hos.shell.chdir(path)
    if not ok then
        hos.stderr("cd: " .. err.message)
        return err.code
    end

    return 0
end
