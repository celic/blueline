module API
	module Entities
		class Team < Grape::Entity

			expose :id, :abbreviation, :full_name, :name, :city, :state, :wins, :division_id, :wins, :losses, :ot, :so, :points

		end
	end
end
