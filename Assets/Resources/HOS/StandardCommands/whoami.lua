function execute(args)
    if #args ~= 0 then
        hos.stderr("Usage: whoami")
        return 2
    end

    hos.stdout(hos.shell.user())
    return 0
end
