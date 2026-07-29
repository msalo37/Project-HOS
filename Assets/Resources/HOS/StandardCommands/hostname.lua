function execute(args)
    if #args ~= 0 then
        hos.stderr("Usage: hostname")
        return 2
    end

    hos.stdout(hos.shell.hostname())
    return 0
end
