function execute(args)
    if #args < 1 then
        hos.stderr("usage: ssh <ip> [port]")
        return 2
    end
    local result, err = hos.net.login(args[1], tonumber(args[2]) or 22)
    if not result then
        hos.stderr(err.message)
        return 1
    end
    hos.stdout(result)
    return 0
end
