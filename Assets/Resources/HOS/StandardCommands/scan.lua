function execute(args)
    if #args < 1 then
        hos.stderr("usage: scan <ip>")
        return 2
    end
    local services, err = hos.net.scan(args[1])
    if not services then
        hos.stderr(err.message)
        return 1
    end
    for _, service in ipairs(services) do
        hos.stdout(service.port .. "/tcp " .. service.protocol .. " " .. service.name .. " " .. service.version)
    end
    return 0
end
