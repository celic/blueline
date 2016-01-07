class CreatePlayers < ActiveRecord::Migration
    def change
        create_table :players do |t|
            t.references :team,     index: true
            t.string :name
            t.integer :position
        end
    end
end
