function execute(args)
    -- Проверяем, пустой ли массив или nil
    if not args or #args == 0 then
        return 4
    end
    
    -- Выводим все элементы массива
    print("Все аргументы массива args:")
    
    for i, value in ipairs(args) do
        print(value)
    end
    
    return 0  -- Успешное завершение
end