function execute(args)
    if not hos.shell.disconnect() then
        hos.stderr("not connected to a remote machine")
        return 1
    end
    return 0
end
