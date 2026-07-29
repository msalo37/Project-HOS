function execute(args)
    if #args ~= 0 then
        hos.stderr("Usage: ps")
        return 2
    end

    hos.stdout("PID STATE NAME")
    for _, process in ipairs(hos.process.list()) do
        hos.stdout(process.pid .. " " .. process.state .. " " .. process.name)
    end

    return 0
end
