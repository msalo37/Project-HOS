function execute(args)
    
    local arr = os.get_files(os.get_current_folder())
    
    for i, v in ipairs(arr) do
        print(i, v)
    end

    return 0
end