function execute(args)
    local recursive = false
    local path = nil

    for _, argument in ipairs(args) do
        if argument == "-r" or argument == "-R" or argument == "--recursive" then
            recursive = true
        elseif path == nil then
            path = argument
        else
            hos.stderr("Usage: rm [-r] <path>")
            return 2
        end
    end

    if path == nil then
        hos.stderr("Usage: rm [-r] <path>")
        return 2
    end

    local ok, err = hos.fs.remove(path, recursive)
    if not ok then
        hos.stderr("rm: " .. err.message)
        return err.code
    end

    return 0
end
