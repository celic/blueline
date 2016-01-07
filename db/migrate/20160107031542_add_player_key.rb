class AddPlayerKey < ActiveRecord::Migration
    def change
        add_column :players, :key, :string, unique: true
        add_index :players, :key, unique: true
    end
end
